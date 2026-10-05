using System.Collections.Frozen;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace Acmi.FeatureFlags.Client;

/// <summary>
/// Hosted service that keeps an in-memory snapshot of feature definitions up to date.
/// </summary>
/// <remarks>
/// Definitions are fetched at startup and then every <c>FeatureFlags:CacheExpirationInMinutes</c> (default 15).
/// Readers use the current snapshot and never wait on HTTP. A failed refresh keeps the last-known-good snapshot.
/// If no refresh has ever succeeded (for example the API is down at startup), there is no snapshot and flags evaluate off.
/// </remarks>
public sealed class FeatureDefinitionRefreshService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<FeatureDefinitionRefreshService> logger,
    TimeProvider? timeProvider = null) : BackgroundService {

    private const double _DefaultRefreshMinutes = 15;
    private static readonly TimeSpan _StartupWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _MinimumRefreshGap = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _RepeatWarningInterval = TimeSpan.FromHours(1);
    private const int _MaxBackoffExponent = 10;

    private readonly IHttpClientFactory _HttpClientFactory = httpClientFactory;
    private readonly IConfiguration _Configuration = configuration;
    private readonly ILogger<FeatureDefinitionRefreshService> _Logger = logger;
    private readonly TimeProvider _TimeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _RefreshSignal = new(0, 1);
    private readonly SemaphoreSlim _RefreshLock = new(1, 1);
    private readonly TaskCompletionSource _InitialRefreshCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Snapshot? _Snapshot;
    private int _ConsecutiveFailures;
    private long _LastWarningTimestamp;

    /// <summary>
    /// Gets whether at least one refresh has succeeded.
    /// </summary>
    public bool HasSnapshot => Volatile.Read(ref _Snapshot) is not null;

    /// <summary>
    /// Gets the definitions in the current snapshot, or an empty list if no refresh has succeeded yet.
    /// </summary>
    public IReadOnlyList<FeatureDefinition> GetDefinitions() => Volatile.Read(ref _Snapshot)?.Definitions ?? [];

    /// <summary>
    /// Gets a definition from the current snapshot by name (case-insensitive), or null if it isn't found.
    /// </summary>
    public FeatureDefinition? GetDefinition(string name)
        => Volatile.Read(ref _Snapshot)?.ByName.GetValueOrDefault(name);

    /// <summary>
    /// Asks the background loop to refresh immediately. The current snapshot stays in place until the refresh succeeds.
    /// </summary>
    /// <remarks>
    /// Requests are coalesced, and refreshes are at least five seconds apart, so calling this repeatedly is cheap and can't hammer the API.
    /// </remarks>
    public void RequestRefresh() {
        try {
            if (_RefreshSignal.CurrentCount > 0) {
                return; // already requested
            }
            _RefreshSignal.Release();
        } catch (SemaphoreFullException) {
            // lost a race with another caller; a refresh is already requested
        } catch (ObjectDisposedException) {
            // host is shutting down; nothing left to refresh
        }
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken) {
        await base.StartAsync(cancellationToken);

        // give the first fetch a short chance to finish so flags are available as soon as the app starts taking requests,
        // but never hold up startup for long when the API is slow or down
        try {
            await _InitialRefreshCompleted.Task.WaitAsync(_StartupWait, cancellationToken);
        } catch (TimeoutException) {
            // continue starting; the refresh keeps running in the background
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await RefreshAsync(stoppingToken);
            var lastRefresh = _TimeProvider.GetTimestamp();
            _InitialRefreshCompleted.TrySetResult();

            while (!stoppingToken.IsCancellationRequested) {
                await _RefreshSignal.WaitAsync(NextDelay(), stoppingToken);

                // enforce a minimum gap between fetches so repeated requests can't hammer the API
                var remaining = _MinimumRefreshGap - _TimeProvider.GetElapsedTime(lastRefresh);
                if (remaining > TimeSpan.Zero) {
                    await Task.Delay(remaining, _TimeProvider, stoppingToken);

                    // requests made while waiting are satisfied by the fetch we're about to do
                    await _RefreshSignal.WaitAsync(0, CancellationToken.None);
                }

                await RefreshAsync(stoppingToken);
                lastRefresh = _TimeProvider.GetTimestamp();
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            // shutting down
        } finally {
            _InitialRefreshCompleted.TrySetResult();
        }
    }

    /// <summary>
    /// Fetches definitions and swaps the snapshot on success. On failure the previous snapshot is kept.
    /// </summary>
    /// <returns>True if the refresh succeeded, else false.</returns>
    /// <remarks>
    /// Only one refresh runs at a time; concurrent callers queue, so an older response can never overwrite a newer snapshot.
    /// </remarks>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default) {
        await _RefreshLock.WaitAsync(cancellationToken);
        try {
            using var timeoutSource = new CancellationTokenSource(_RequestTimeout, _TimeProvider);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

            var httpClient = _HttpClientFactory.CreateClient(Constants.HttpClientName);
            using var response = await httpClient.GetAsync("features", timeout.Token);
            response.EnsureSuccessStatusCode();

            var featureFlags = await response.Content.ReadFromJsonAsync<List<CustomFeatureDefinition>>(timeout.Token) ?? [];
            Volatile.Write(ref _Snapshot, new Snapshot(featureFlags.Select(FeatureDefinitionMapper.ToFeatureDefinition).ToArray()));

            if (Interlocked.Exchange(ref _ConsecutiveFailures, 0) > 0) {
                _Logger.LogInformation("Feature definition refresh recovered");
            }
            return true;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LogRefreshFailure(ex);
            return false;
        } finally {
            _RefreshLock.Release();
        }
    }

    private TimeSpan RefreshInterval {
        get {
            var minutes = _Configuration.GetValue("FeatureFlags:CacheExpirationInMinutes", _DefaultRefreshMinutes);
            return TimeSpan.FromMinutes(minutes > 0 ? minutes : _DefaultRefreshMinutes);
        }
    }

    // normal interval when healthy; while failing, retry with exponential backoff capped at the normal interval
    private TimeSpan NextDelay() {
        var interval = RefreshInterval;
        var failures = Volatile.Read(ref _ConsecutiveFailures);
        if (failures == 0) {
            return interval;
        }
        var backoff = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(failures, _MaxBackoffExponent)));
        return backoff < interval ? backoff : interval;
    }

    // log the first failure, then at most once per hour while the outage continues
    private void LogRefreshFailure(Exception ex) {
        var failures = Interlocked.Increment(ref _ConsecutiveFailures);
        var now = _TimeProvider.GetTimestamp();
        if (failures > 1 && _TimeProvider.GetElapsedTime(Volatile.Read(ref _LastWarningTimestamp), now) < _RepeatWarningInterval) {
            return;
        }
        Volatile.Write(ref _LastWarningTimestamp, now);

        var reason = ex is OperationCanceledException ? "request timed out" : ex.Message;
        _Logger.LogWarning(
            "Failed to refresh feature definitions ({Reason}); {State}. Consecutive failures: {Failures}",
            reason,
            HasSnapshot ? "keeping last-known-good definitions" : "no definitions loaded yet, all flags evaluate off",
            failures);
    }

    /// <inheritdoc />
    public override void Dispose() {
        _RefreshSignal.Dispose();
        _RefreshLock.Dispose();
        base.Dispose();
    }

    // immutable once built; swapped atomically
    private sealed class Snapshot {
        public Snapshot(FeatureDefinition[] definitions) {
            Definitions = definitions;
            var byName = new Dictionary<string, FeatureDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions) {
                byName.TryAdd(definition.Name, definition);
            }
            ByName = byName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<FeatureDefinition> Definitions { get; }
        public FrozenDictionary<string, FeatureDefinition> ByName { get; }
    }
}

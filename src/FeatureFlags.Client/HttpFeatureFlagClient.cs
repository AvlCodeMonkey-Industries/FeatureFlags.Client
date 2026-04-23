using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Client;

/// <inheritdoc />
public class HttpFeatureFlagClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, IMemoryCache memoryCache, ILogger<HttpFeatureFlagClient> logger) : IFeatureFlagClient {
    private readonly IHttpClientFactory _HttpClientFactory = httpClientFactory;
    private readonly IConfiguration _Configuration = configuration;
    private readonly IMemoryCache _MemoryCache = memoryCache;
    private readonly ILogger<HttpFeatureFlagClient> _Logger = logger;

    /// <inheritdoc />
    public async Task<List<FeatureDefinition>> GetAllFeatureDefinitionsAsync(CancellationToken cancellationToken = default) {
        try {
            var definitions = await _MemoryCache.GetOrCreateAsync(Constants.FeatureDefinitionsCacheKey, async entry => {
                entry.AbsoluteExpirationRelativeToNow = CacheTimeSpan;
                return await FetchFeatureDefinitionsAsync(cancellationToken);
            });

            return definitions ?? [];
        } catch (Exception ex) {
            _Logger.LogError(ex, "Error getting feature definitions");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<FeatureDefinition?> GetFeatureDefinitionByNameAsync(string name, CancellationToken cancellationToken = default) {
        try {
            var definitions = await _MemoryCache.GetOrCreateAsync(Constants.FeatureDefinitionsCacheKey, async entry => {
                entry.AbsoluteExpirationRelativeToNow = CacheTimeSpan;
                return await FetchFeatureDefinitionsAsync(cancellationToken);
            });
            return definitions?.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        } catch (Exception ex) {
            _Logger.LogError(ex, "Error getting feature definition for '{Name}'", name);
            return null;
        }
    }

    /// <inheritdoc />
    public bool ClearCache() {
        _MemoryCache.Remove(Constants.FeatureDefinitionsCacheKey);
        return true;
    }

    private TimeSpan? _CacheTimeSpan;
    private TimeSpan CacheTimeSpan => _CacheTimeSpan ??= TimeSpan.FromMinutes(_Configuration.GetValue("FeatureFlags:CacheExpirationInMinutes", 15));

    private async Task<List<FeatureDefinition>> FetchFeatureDefinitionsAsync(CancellationToken cancellationToken = default) {
        try {
            var httpClient = _HttpClientFactory.CreateClient(Constants.HttpClientName);
            using var response = await httpClient.GetAsync("features", cancellationToken);
            response.EnsureSuccessStatusCode();

            var featureFlags = await response.Content.ReadFromJsonAsync<List<CustomFeatureDefinition>>(cancellationToken) ?? [];
            return featureFlags.Select(FeatureDefinitionMapper.ToFeatureDefinition).ToList();
        } catch (Exception ex) {
            _Logger.LogError(ex, "Error fetching feature definitions");
            return [];
        }
    }
}

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acmi.FeatureFlags.Client.Tests;

public class FeatureDefinitionRefreshServiceTests {
    private readonly Mock<ILogger<FeatureDefinitionRefreshService>> _LoggerMock = new();

    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Interlocked.Increment(ref Calls);
            return respond(cancellationToken);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider {
        private long _Timestamp;
        public override long GetTimestamp() => Interlocked.Read(ref _Timestamp);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan by) => Interlocked.Add(ref _Timestamp, by.Ticks);
    }

    private sealed class TimeoutTimeProvider : TimeProvider {
        private readonly List<FakeTimer> _Timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            if (dueTime != TimeSpan.FromSeconds(30)) {
                return base.CreateTimer(callback, state, dueTime, period);
            }
            var timer = new FakeTimer(() => callback(state));
            lock (_Timers) {
                _Timers.Add(timer);
            }
            return timer;
        }

        public int TimerCount {
            get {
                lock (_Timers) {
                    return _Timers.Count;
                }
            }
        }

        public void FireTimeouts() {
            FakeTimer[] timers;
            lock (_Timers) {
                timers = [.. _Timers];
                _Timers.Clear();
            }
            foreach (var timer in timers) {
                timer.Fire();
            }
        }

        private sealed class FakeTimer(Action fire) : ITimer {
            private int _Disposed;
            public void Fire() {
                if (Volatile.Read(ref _Disposed) == 0) {
                    fire();
                }
            }
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() => Interlocked.Exchange(ref _Disposed, 1);
            public ValueTask DisposeAsync() {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    // the API serves the Microsoft Feature Management schema
    private static HttpResponseMessage OkJson(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Ok(params string[] names)
        => OkJson($$"""{ "feature_management": { "feature_flags": [ {{string.Join(",", names.Select(n => $$"""{ "id": "{{n}}", "enabled": true }"""))}} ] } }""");

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private FeatureDefinitionRefreshService CreateService(StubHandler handler, TimeProvider? timeProvider = null, string minutes = "15") {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(Constants.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost/") });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "FeatureFlags:CacheExpirationInMinutes", minutes } })
            .Build();
        return new FeatureDefinitionRefreshService(factory.Object, configuration, _LoggerMock.Object, timeProvider);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null) {
        var deadline = DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(10));
        while (!condition()) {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for condition");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private void VerifyWarnings(Times times)
        => _LoggerMock.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    [Fact]
    public async Task RefreshAsync_Success_ReplacesSnapshot() {
        var responses = new Queue<HttpResponseMessage>([Ok("A"), Ok("B", "C")]);
        var service = CreateService(new StubHandler(_ => Task.FromResult(responses.Dequeue())));

        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["A"], service.GetDefinitions().Select(d => d.Name));

        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["B", "C"], service.GetDefinitions().Select(d => d.Name));
        Assert.Null(service.GetDefinition("A"));
        Assert.NotNull(service.GetDefinition("c"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RefreshAsync_BadStatus_KeepsPreviousSnapshot(HttpStatusCode failure) {
        var responses = new Queue<HttpResponseMessage>([Ok("A"), Status(failure)]);
        var service = CreateService(new StubHandler(_ => Task.FromResult(responses.Dequeue())));
        await service.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(await service.RefreshAsync(TestContext.Current.CancellationToken));

        Assert.Equal(["A"], service.GetDefinitions().Select(d => d.Name));
        VerifyWarnings(Times.Once());
    }

    [Fact]
    public async Task RefreshAsync_NetworkError_KeepsPreviousSnapshot() {
        var calls = 0;
        var service = CreateService(new StubHandler(_ => ++calls == 1 ? Task.FromResult(Ok("A")) : throw new HttpRequestException("boom")));
        await service.RefreshAsync(TestContext.Current.CancellationToken);

        Assert.False(await service.RefreshAsync(TestContext.Current.CancellationToken));

        Assert.Equal(["A"], service.GetDefinitions().Select(d => d.Name));
    }

    [Fact]
    public async Task RefreshAsync_RecoversAfterOutage() {
        var responses = new Queue<HttpResponseMessage>([Ok("A"), Status(HttpStatusCode.ServiceUnavailable), Status(HttpStatusCode.ServiceUnavailable), Ok("A", "B")]);
        var service = CreateService(new StubHandler(_ => Task.FromResult(responses.Dequeue())));

        await service.RefreshAsync(TestContext.Current.CancellationToken);
        await service.RefreshAsync(TestContext.Current.CancellationToken);
        await service.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Single(service.GetDefinitions());

        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["A", "B"], service.GetDefinitions().Select(d => d.Name));
    }

    [Fact]
    public async Task RefreshAsync_RepeatedFailures_LogsFirstThenThrottles() {
        var time = new ManualTimeProvider();
        var service = CreateService(new StubHandler(_ => Task.FromResult(Status(HttpStatusCode.BadGateway))), time);

        await service.RefreshAsync(TestContext.Current.CancellationToken);
        await service.RefreshAsync(TestContext.Current.CancellationToken);
        await service.RefreshAsync(TestContext.Current.CancellationToken);
        VerifyWarnings(Times.Once());

        time.Advance(TimeSpan.FromHours(1));
        await service.RefreshAsync(TestContext.Current.CancellationToken);
        VerifyWarnings(Times.Exactly(2));
    }

    [Fact]
    public async Task ColdStart_ApiDown_EvaluatesOffWithoutThrowing() {
        var service = CreateService(new StubHandler(_ => throw new HttpRequestException("down")));
        var client = new HttpFeatureFlagClient(service);

        Assert.False(await service.RefreshAsync(TestContext.Current.CancellationToken));

        Assert.False(service.HasSnapshot);
        Assert.Empty(await client.GetAllFeatureDefinitionsAsync(TestContext.Current.CancellationToken));
        Assert.Null(await client.GetFeatureDefinitionByNameAsync("A", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reads_DoNotBlockOnInFlightRefresh() {
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var handler = new StubHandler(_ => ++calls == 1 ? Task.FromResult(Ok("A")) : gate.Task);
        var service = CreateService(handler);
        var client = new HttpFeatureFlagClient(service);
        await service.RefreshAsync(TestContext.Current.CancellationToken);

        var inFlight = service.RefreshAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => handler.Calls == 2);

        var read = client.GetAllFeatureDefinitionsAsync(TestContext.Current.CancellationToken);
        Assert.True(read.IsCompletedSuccessfully);
        Assert.Equal(["A"], (await read).Select(d => d.Name));
        Assert.False(inFlight.IsCompleted);

        gate.SetResult(Ok("B"));
        await inFlight;
        Assert.Equal(["B"], service.GetDefinitions().Select(d => d.Name));
    }

    [Fact]
    public async Task HostedService_RefreshesAtStartup_AndStopsCleanly() {
        var handler = new StubHandler(_ => Task.FromResult(Ok("A")));
        var service = CreateService(handler);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(service.HasSnapshot);
        await service.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task HostedService_RefreshesOnInterval() {
        var handler = new StubHandler(_ => Task.FromResult(Ok("A")));
        var service = CreateService(handler, minutes: "0.0002");

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => handler.Calls >= 3, TimeSpan.FromSeconds(20));
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ClearCache_TriggersImmediateRefresh() {
        var calls = 0;
        var handler = new StubHandler(_ => Task.FromResult(++calls == 1 ? Ok("A") : Ok("B")));
        var service = CreateService(handler);
        var client = new HttpFeatureFlagClient(service);
        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(client.ClearCache());
        await WaitUntilAsync(() => service.GetDefinition("B") is not null);

        Assert.Equal(["B"], service.GetDefinitions().Select(d => d.Name));
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ClearCache_DuringOutage_KeepsOldValues() {
        var calls = 0;
        var handler = new StubHandler(_ => Task.FromResult(++calls == 1 ? Ok("A") : Status(HttpStatusCode.InternalServerError)));
        var service = CreateService(handler);
        var client = new HttpFeatureFlagClient(service);
        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(client.ClearCache());
        await WaitUntilAsync(() => handler.Calls >= 2);

        var definitions = await client.GetAllFeatureDefinitionsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["A"], definitions.Select(d => d.Name));
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestRefresh_RepeatedCalls_AreCoalescedAndRateLimited() {
        var handler = new StubHandler(_ => Task.FromResult(Ok("A")));
        var service = CreateService(handler);
        await service.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls);

        for (var i = 0; i < 1000; i++) {
            service.RequestRefresh();
        }

        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls); // still inside the minimum gap
        await WaitUntilAsync(() => handler.Calls == 2);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Calls); // the 1000 requests produced a single fetch
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestRefresh_AfterDispose_DoesNotThrow() {
        var service = CreateService(new StubHandler(_ => Task.FromResult(Ok("A"))));
        service.Dispose();

        var exception = Record.Exception(service.RequestRefresh);

        Assert.Null(exception);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCalls_RunOneAtATime() {
        var running = 0;
        var maxRunning = 0;
        var handler = new StubHandler(async ct => {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            await Task.Delay(50, ct);
            Interlocked.Decrement(ref running);
            return Ok("A");
        });
        var service = CreateService(handler);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.RefreshAsync(TestContext.Current.CancellationToken)));

        Assert.Equal(5, handler.Calls);
        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task HostedService_ApiDownAtStartup_RetriesWithBackoffInsteadOfWaitingFullInterval() {
        var calls = 0;
        var handler = new StubHandler(_ => Task.FromResult(++calls == 1 ? Status(HttpStatusCode.ServiceUnavailable) : Ok("A")));
        var service = CreateService(handler); // 15 minute interval

        await service.StartAsync(TestContext.Current.CancellationToken);
        Assert.False(service.HasSnapshot);

        await WaitUntilAsync(() => service.HasSnapshot);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    private static void InterlockedMax(ref int target, int value) {
        int current;
        while (value > (current = Volatile.Read(ref target))) {
            if (Interlocked.CompareExchange(ref target, value, current) == current) {
                return;
            }
        }
    }

    [Fact]
    public async Task StartAsync_DoesNotHangWhenApiNeverResponds() {
        var handler = new StubHandler(async ct => {
            await Task.Delay(Timeout.Infinite, ct);
            return Ok();
        });
        var service = CreateService(handler);

        var started = service.StartAsync(TestContext.Current.CancellationToken);
        await started.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        Assert.False(service.HasSnapshot);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RefreshAsync_RequestTimeout_ReturnsFalseKeepsSnapshotAndAllowsRetry() {
        var time = new TimeoutTimeProvider();
        var call = 0;
        var handler = new StubHandler(async ct => {
            switch (Interlocked.Increment(ref call)) {
                case 1:
                    return Ok("A");
                case 2:
                    await Task.Delay(Timeout.Infinite, ct);
                    return Ok();
                default:
                    return Ok("B");
            }
        });
        var service = CreateService(handler, time);
        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));

        var timedOut = service.RefreshAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => time.TimerCount > 0 && Volatile.Read(ref handler.Calls) == 2);
        time.FireTimeouts();

        Assert.False(await timedOut.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Equal(["A"], service.GetDefinitions().Select(d => d.Name));
        VerifyWarnings(Times.Once());

        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["B"], service.GetDefinitions().Select(d => d.Name));
    }
}

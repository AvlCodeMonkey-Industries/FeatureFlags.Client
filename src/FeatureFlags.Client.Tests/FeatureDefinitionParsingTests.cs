using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Moq;

namespace Acmi.FeatureFlags.Client.Tests;

/// <summary>
/// Verifies the client against the JSON the FeatureFlags.app <c>features</c> endpoint serves (Microsoft Feature Management schema).
/// </summary>
public class FeatureDefinitionParsingTests {
    private const string _ApiJson = """
        {
          "feature_management": {
            "feature_flags": [
              { "id": "on", "enabled": true },
              { "id": "off", "enabled": false },
              {
                "id": "targeted",
                "enabled": true,
                "conditions": {
                  "requirement_type": "Any",
                  "client_filters": [
                    { "name": "Microsoft.Targeting", "parameters": { "Audience": { "Users": ["alice", "bob"], "DefaultRolloutPercentage": 0, "Exclusion": { "Users": ["bob"] } } } },
                    { "name": "Microsoft.TimeWindow", "parameters": { "Start": "Mon, 05 Jan 2026 09:00:00 GMT", "End": "Mon, 05 Jan 2026 17:00:00 GMT",
                      "Recurrence": { "Pattern": { "Type": "Weekly", "Interval": 2, "DaysOfWeek": ["Monday", "Friday"], "FirstDayOfWeek": "Sunday" }, "Range": { "Type": "Numbered", "NumberOfOccurrences": 10 } } } }
                  ]
                }
              },
              { "id": "custom", "enabled": true, "conditions": { "client_filters": [ { "name": "Test.Level", "parameters": { "Level": 5 } } ] } }
            ]
          }
        }
        """;

    private sealed class CapturingHandler(string json) : HttpMessageHandler {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
        }
    }

    [FilterAlias("Test.Level")]
    private sealed class LevelFilter : IFeatureFilter {
        public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context) => Task.FromResult(context.Parameters.GetValue<int>("Level") >= 3);
    }

    private sealed class StubProvider(IReadOnlyList<FeatureDefinition> definitions) : IFeatureDefinitionProvider {
        public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync() {
            foreach (var definition in definitions) {
                yield return definition;
            }
            await Task.CompletedTask;
        }

        public Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
            => Task.FromResult(definitions.FirstOrDefault(x => x.Name == featureName) ?? new FeatureDefinition { Name = featureName });
    }

    private static async Task<(FeatureDefinitionRefreshService Service, CapturingHandler Handler)> RefreshAsync(string json) {
        var handler = new CapturingHandler(json);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(Constants.HttpClientName)).Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://featureflags.app/api/") });
        var service = new FeatureDefinitionRefreshService(factory.Object, new ConfigurationBuilder().Build(), Mock.Of<ILogger<FeatureDefinitionRefreshService>>());
        Assert.True(await service.RefreshAsync(TestContext.Current.CancellationToken));
        return (service, handler);
    }

    private static IFeatureManager CreateManager(FeatureDefinitionRefreshService service) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddFeatureManagement().AddFeatureFilter<LevelFilter>().WithTargeting();
        services.RemoveAll<IFeatureDefinitionProvider>();
        services.AddSingleton<IFeatureDefinitionProvider>(new StubProvider(service.GetDefinitions()));
        return services.BuildServiceProvider().GetRequiredService<IFeatureManager>();
    }

    [Fact]
    public async Task Refresh_RequestsTheFeaturesEndpoint() {
        var (_, handler) = await RefreshAsync(_ApiJson);

        Assert.Equal(new Uri("https://featureflags.app/api/features"), handler.RequestUri);
    }

    [Fact]
    public async Task Refresh_ParsesApiJsonIntoFeatureDefinitions() {
        var (service, _) = await RefreshAsync(_ApiJson);

        Assert.Equal(["on", "off", "targeted", "custom"], service.GetDefinitions().Select(x => x.Name));
        Assert.Equal(FeatureStatus.Disabled, service.GetDefinition("off")!.Status);

        var targeted = service.GetDefinition("targeted")!;
        Assert.Equal(RequirementType.Any, targeted.RequirementType);
        var filters = targeted.EnabledFor.ToList();
        Assert.Equal(["Microsoft.Targeting", "Microsoft.TimeWindow"], filters.Select(x => x.Name));
        Assert.Equal("alice", filters[0].Parameters["Audience:Users:0"]);
        Assert.Equal("Weekly", filters[1].Parameters["Recurrence:Pattern:Type"]);

        // custom filters see their parameters under their own names
        Assert.Equal("5", service.GetDefinition("custom")!.EnabledFor.Single().Parameters["Level"]);
    }

    [Fact]
    public async Task Refresh_EvaluatesFlagsLikePlainMicrosoftFeatureManagement() {
        var (service, _) = await RefreshAsync(_ApiJson);
        var manager = CreateManager(service);

        Assert.True(await manager.IsEnabledAsync("on", new TargetingContext { UserId = "alice" }));
        Assert.False(await manager.IsEnabledAsync("off", new TargetingContext { UserId = "alice" }));
        Assert.True(await manager.IsEnabledAsync("targeted", new TargetingContext { UserId = "alice" }));
        Assert.False(await manager.IsEnabledAsync("targeted", new TargetingContext { UserId = "bob" }));
        Assert.True(await manager.IsEnabledAsync("custom", new TargetingContext { UserId = "alice" }));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(70)]
    [InlineData(95)]
    public async Task Percentage_UsesTargetingWithUniformDistribution_ForSimilarLookingIdentities(int percentage) {
        var json = $$"""
            { "feature_management": { "feature_flags": [ { "id": "rollout", "enabled": true, "conditions": { "requirement_type": "All", "client_filters": [
              { "name": "Microsoft.Targeting", "parameters": { "Audience": { "Users": [], "DefaultRolloutPercentage": {{percentage}} } } } ] } } ] } }
            """;
        var (service, _) = await RefreshAsync(json);
        var manager = CreateManager(service);

        const int users = 4000;
        var enabled = 0;
        var first = new List<bool>();
        for (var i = 0; i < users; i++) {
            // the old character-sum bucketing was off by 10-30 points for identities shaped like this
            var result = await manager.IsEnabledAsync("rollout", new TargetingContext { UserId = $"user-{i}@example.com" });
            first.Add(result);
            enabled += result ? 1 : 0;
        }

        Assert.InRange(enabled * 100.0 / users, percentage - 3, percentage + 3);

        // each user stays in or out of the rollout on every evaluation
        for (var i = 0; i < 200; i++) {
            Assert.Equal(first[i], await manager.IsEnabledAsync("rollout", new TargetingContext { UserId = $"user-{i}@example.com" }));
        }
    }

    [Fact]
    public async Task Refresh_EmptyFlagList_ReplacesSnapshotWithNoDefinitions() {
        var (service, _) = await RefreshAsync("""{ "feature_management": { "feature_flags": [] } }""");

        Assert.True(service.HasSnapshot);
        Assert.Empty(service.GetDefinitions());
    }
}

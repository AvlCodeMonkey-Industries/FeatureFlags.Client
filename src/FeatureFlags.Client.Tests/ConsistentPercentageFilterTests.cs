using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Moq;

namespace Acmi.FeatureFlags.Client.Tests;

public class ConsistentPercentageFilterTests {
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(100, true)]
    public async Task EvaluateAsync_AnonymousUser_ReturnsExpectedResult(int value, bool expected) {
        // arrange
        var httpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // anonymous
        };
        var httpAccessorMock = new Mock<IHttpContextAccessor>();
        httpAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var filter = new ConsistentPercentageFilter(httpAccessorMock.Object, null);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Value"] = value.ToString() })
            .Build();
        var context = new FeatureFilterEvaluationContext { FeatureName = "TestFeature", Parameters = config };

        // act
        var result = await filter.EvaluateAsync(context);

        // assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task EvaluateAsync_KnownUser_ComputesConsistentPercentage() {
        // arrange
        var name = "A"; // ASCII 65 -> percent 65
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, name) });
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext {
            User = principal
        };

        var httpAccessorMock = new Mock<IHttpContextAccessor>();
        httpAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var filter = new ConsistentPercentageFilter(httpAccessorMock.Object, null);

        // threshold just above percent -> should be enabled
        var configEnabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Value"] = "66" })
            .Build();
        var ctxEnabled = new FeatureFilterEvaluationContext { FeatureName = "TestFeature", Parameters = configEnabled };

        // threshold equal to percent -> not enabled (percent < value)
        var configDisabled = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Value"] = "65" })
            .Build();
        var ctxDisabled = new FeatureFilterEvaluationContext { FeatureName = "TestFeature", Parameters = configDisabled };

        // act
        var resultEnabled = await filter.EvaluateAsync(ctxEnabled);
        var resultDisabled = await filter.EvaluateAsync(ctxDisabled);

        // assert
        Assert.True(resultEnabled);
        Assert.False(resultDisabled);
    }

    [Fact]
    public async Task EvaluateAsync_UsesPreboundSettings_WhenSettingsProvided() {
        // arrange
        var name = "AB"; // 65 + 66 = 131 -> %100 = 31
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, name) });
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext {
            User = principal
        };

        var httpAccessorMock = new Mock<IHttpContextAccessor>();
        httpAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        var filter = new ConsistentPercentageFilter(httpAccessorMock.Object, null);

        var prebound = new PercentageFilterSettings { Value = 32 }; // greater than 31 -> enabled
        var context = new FeatureFilterEvaluationContext { FeatureName = "FeatureX", Settings = prebound };

        // act
        var result = await filter.EvaluateAsync(context);

        // assert
        Assert.True(result);
    }
}

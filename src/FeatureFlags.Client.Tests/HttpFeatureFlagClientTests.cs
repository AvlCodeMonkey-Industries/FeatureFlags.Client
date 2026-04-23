using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace FeatureFlags.Client.Tests;

public class HttpFeatureFlagClientTests {
    [Fact]
    public async Task GetAllFeatureDefinitionsAsync_ReturnsFeatureDefinitions_UsesCache() {
        // Arrange
        var customFeatures = new List<CustomFeatureDefinition>
        {
            new() { Name = "FeatureA" },
            new() { Name = "FeatureB" }
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith("features")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(customFeatures)
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(Constants.HttpClientName)).Returns(httpClient);

        var loggerMock = new Mock<ILogger<HttpFeatureFlagClient>>();
        var configurationManager = new ConfigurationManager();
        configurationManager.AddInMemoryCollection(new Dictionary<string, string?> {
            { "FeatureFlags:CacheExpirationInMinutes", "15" }
        });

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new HttpFeatureFlagClient(httpClientFactoryMock.Object, configurationManager, memoryCache, loggerMock.Object);

        // Act
        var result1 = await client.GetAllFeatureDefinitionsAsync(TestContext.Current.CancellationToken);
        var result2 = await client.GetAllFeatureDefinitionsAsync(TestContext.Current.CancellationToken); // Should hit cache

        // Assert
        Assert.NotNull(result1);
        Assert.Equal(2, result1.Count);
        Assert.Contains(result1, f => f.Name == "FeatureA");
        Assert.Contains(result1, f => f.Name == "FeatureB");
        Assert.Equal(result1, result2); // Cached result should be the same
        handlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith("features")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAllFeatureDefinitionsAsync_HandlesApiError_LogsAndReturnsEmpty() {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(Constants.HttpClientName)).Returns(httpClient);

        var loggerMock = new Mock<ILogger<HttpFeatureFlagClient>>();
        var configurationManager = new ConfigurationManager();
        configurationManager.AddInMemoryCollection(new Dictionary<string, string?> {
            { "FeatureFlags:CacheExpirationInMinutes", "15" }
        });

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new HttpFeatureFlagClient(httpClientFactoryMock.Object, configurationManager, memoryCache, loggerMock.Object);

        // Act
        var result = await client.GetAllFeatureDefinitionsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error fetching feature definitions")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce()
        );
        handlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith("features")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureDefinitionByNameAsync_ReturnsFeatureDefinition_FromCache() {
        // Arrange
        var customFeatures = new List<CustomFeatureDefinition>
        {
            new() { Name = "FeatureX" },
            new() { Name = "FeatureY" }
        };
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(customFeatures)
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(Constants.HttpClientName)).Returns(httpClient);

        var loggerMock = new Mock<ILogger<HttpFeatureFlagClient>>();
        var configurationManager = new ConfigurationManager();
        configurationManager.AddInMemoryCollection(new Dictionary<string, string?> {
            { "FeatureFlags:CacheExpirationInMinutes", "15" }
        });

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new HttpFeatureFlagClient(httpClientFactoryMock.Object, configurationManager, memoryCache, loggerMock.Object);

        // Act
        var result = await client.GetFeatureDefinitionByNameAsync("FeatureX", TestContext.Current.CancellationToken);
        var result2 = await client.GetFeatureDefinitionByNameAsync("FeatureX", TestContext.Current.CancellationToken); // should hit cache

        // Assert
        Assert.NotNull(result);
        Assert.Equal("FeatureX", result.Name);
        Assert.Equal(result, result2);
        handlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith("features")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetFeatureDefinitionByNameAsync_HandlesApiError_LogsAndReturnsNull() {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(Constants.HttpClientName)).Returns(httpClient);

        var loggerMock = new Mock<ILogger<HttpFeatureFlagClient>>();
        var configurationManager = new ConfigurationManager();
        configurationManager.AddInMemoryCollection(new Dictionary<string, string?> {
            { "FeatureFlags:CacheExpirationInMinutes", "15" }
        });

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var client = new HttpFeatureFlagClient(httpClientFactoryMock.Object, configurationManager, memoryCache, loggerMock.Object);

        // Act
        var result = await client.GetFeatureDefinitionByNameAsync("MissingFeature", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error fetching feature definitions") || v.ToString()!.Contains("Error getting feature definition")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce()
        );
        handlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().EndsWith("features")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void ClearCache_ClearsCache() {
        // Arrange
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        var loggerMock = new Mock<ILogger<HttpFeatureFlagClient>>();
        var configurationManager = new ConfigurationManager();
        configurationManager.AddInMemoryCollection(new Dictionary<string, string?> {
            { "FeatureFlags:CacheExpirationInMinutes", "15" }
        });
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        memoryCache.Set(Constants.FeatureDefinitionsCacheKey, ""); // Ensure cache entry exists

        // Act
        var result1 = memoryCache.TryGetValue(Constants.FeatureDefinitionsCacheKey, out _);
        var client = new HttpFeatureFlagClient(httpClientFactoryMock.Object, configurationManager, memoryCache, loggerMock.Object);
        client.ClearCache();
        var result2 = memoryCache.TryGetValue(Constants.FeatureDefinitionsCacheKey, out _);

        // Assert
        Assert.True(result1);
        Assert.False(result2);
    }
}

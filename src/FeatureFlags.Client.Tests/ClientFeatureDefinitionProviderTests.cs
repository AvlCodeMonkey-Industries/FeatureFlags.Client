using Microsoft.FeatureManagement;
using Moq;

namespace FeatureFlags.Client.Tests;

public class ClientFeatureDefinitionProviderTests {
    private readonly Mock<IFeatureFlagClient> _FeatureFlagClientMock;
    private readonly ClientFeatureDefinitionProvider _Provider;

    public ClientFeatureDefinitionProviderTests() {
        _FeatureFlagClientMock = new Mock<IFeatureFlagClient>();
        _Provider = new ClientFeatureDefinitionProvider(_FeatureFlagClientMock.Object);
    }

    [Fact]
    public void GetAllFeatureDefinitionsAsync_ReturnsAllFeatureDefinitions() {
        // Arrange
        var featureDefinitions = new List<FeatureDefinition>
        {
            new() { Name = "Feature1", EnabledFor = new List<FeatureFilterConfiguration> { new() { Name = "AlwaysOn" } } },
            new() { Name = "Feature2", EnabledFor = new List<FeatureFilterConfiguration> { new() { Name = "AlwaysOn" } } }
        };
        _FeatureFlagClientMock.Setup(s => s.GetAllFeatureDefinitionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(featureDefinitions);

        // Act
        var result = _Provider.GetAllFeatureDefinitionsAsync();

        // Assert
        var definitions = result.ToBlockingEnumerable(TestContext.Current.CancellationToken).ToList();
        Assert.Equal(2, definitions.Count);
        Assert.Equal("Feature1", definitions[0].Name);
        Assert.NotNull(definitions[0].EnabledFor);
        Assert.Equal("Feature2", definitions[1].Name);
        Assert.NotNull(definitions[1].EnabledFor);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_ReturnsFeatureDefinition_WhenFeatureExists() {
        // Arrange
        var featureDefinition = new FeatureDefinition { Name = "Feature1", EnabledFor = new List<FeatureFilterConfiguration> { new() { Name = "AlwaysOn" } } };
        _FeatureFlagClientMock.Setup(s => s.GetFeatureDefinitionByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(featureDefinition);

        // Act
        var result = await _Provider.GetFeatureDefinitionAsync("Feature1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Feature1", result.Name);
        Assert.NotNull(result.EnabledFor);
    }

    [Fact]
    public async Task GetFeatureDefinitionAsync_ReturnsEmptyFeatureDefinition_WhenFeatureDoesNotExist() {
        // Arrange
        _FeatureFlagClientMock.Setup(s => s.GetFeatureDefinitionByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(null as FeatureDefinition);

        // Act
        var result = await _Provider.GetFeatureDefinitionAsync("NonExistentFeature");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("NonExistentFeature", result.Name);
        Assert.Empty(result.EnabledFor);
    }
}

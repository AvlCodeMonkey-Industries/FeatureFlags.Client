using Microsoft.FeatureManagement;

namespace Acmi.FeatureFlags.Client.Tests;

public class FeatureDefinitionMapperTests {
    [Fact]
    public void ToFeatureDefinition_ReturnsDefinition() {
        var customFeatureDefinition = new CustomFeatureDefinition {
            Name = "feature name",
            EnabledFor = [new CustomFeatureFilterConfiguration { Name = "filter name" }],
            Variants = [new VariantDefinition { Name = "variant name" }],
        };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        Assert.Equal(customFeatureDefinition.Name, definition.Name);
        Assert.NotNull(definition.EnabledFor);
        Assert.Single(definition.EnabledFor);
        Assert.Equal("filter name", definition.EnabledFor.First().Name);
        Assert.NotNull(definition.Variants);
        Assert.Single(definition.Variants);
        Assert.Equal("variant name", definition.Variants.First().Name);
    }

    [Fact]
    public void ToFeatureDefinition_WithEmptyEnabledFor_ReturnsDefinition() {
        var customFeatureDefinition = new CustomFeatureDefinition {
            Name = "feature name"
        };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        Assert.Equal(customFeatureDefinition.Name, definition.Name);
        Assert.NotNull(definition.EnabledFor);
        Assert.Empty(definition.EnabledFor);
    }

    [Fact]
    public void ToFeatureDefinition_WithMultipleEnabledFor_ReturnsAllFilters() {
        var customFeatureDefinition = new CustomFeatureDefinition {
            Name = "feature name",
            EnabledFor =
            [
                new CustomFeatureFilterConfiguration { Name = "filter1" },
                new CustomFeatureFilterConfiguration { Name = "filter2" }
            ]
        };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        Assert.Equal(customFeatureDefinition.Name, definition.Name);
        Assert.NotNull(definition.EnabledFor);
        Assert.Equal(2, definition.EnabledFor.Count());
        Assert.Contains(definition.EnabledFor, f => f.Name == "filter1");
        Assert.Contains(definition.EnabledFor, f => f.Name == "filter2");
    }

    [Fact]
    public void ToFeatureDefinition_WithNullInput_ThrowsArgumentNullException()
        => Assert.Throws<NullReferenceException>(() => FeatureDefinitionMapper.ToFeatureDefinition(null!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToFeatureDefinition_WithNullOrEmptyName_MapsName(string? name) {
        var customFeatureDefinition = new CustomFeatureDefinition {
            Name = name!
        };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        Assert.Equal(name, definition.Name);
    }

    [Fact]
    public void ToFeatureDefinition_WithNullEnabledFor_ReturnsNullEnabledFor() {
        var customFeatureDefinition = new CustomFeatureDefinition {
            Name = "feature",
            EnabledFor = null!
        };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        Assert.Null(definition.EnabledFor);
    }

    [Fact]
    public void ToFeatureDefinition_WithNullVariants_ReturnsEmptyVariants() {
        var customFeatureDefinition = new CustomFeatureDefinition {
            Name = "feature",
            Variants = null!
        };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        Assert.NotNull(definition.Variants);
        Assert.Empty(definition.Variants);
    }

    [Fact]
    public void ToFeatureDefinition_WithMultipleVariants_ReturnsAllVariants() {
        var customFeatureDefinition = new CustomFeatureDefinition {
            Name = "feature",
            Variants =
            [
                new VariantDefinition { Name = "variant1" },
                new VariantDefinition { Name = "variant2" }
            ]
        };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        Assert.NotNull(definition.Variants);
        Assert.Equal(2, definition.Variants.Count());
        Assert.Contains(definition.Variants, v => v.Name == "variant1");
        Assert.Contains(definition.Variants, v => v.Name == "variant2");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToFeatureDefinition_WithNullSubItemName_MapsToNullName(bool isFilter) {
        var customFeatureDefinition = isFilter
            ? new CustomFeatureDefinition {
                Name = "feature",
                EnabledFor = [new CustomFeatureFilterConfiguration { Name = null! }]
            }
            : new CustomFeatureDefinition {
                Name = "feature",
                Variants = [new VariantDefinition { Name = null }]
            };

        var definition = FeatureDefinitionMapper.ToFeatureDefinition(customFeatureDefinition);

        Assert.NotNull(definition);
        if (isFilter) {
            Assert.Single(definition.EnabledFor);
            Assert.Null(definition.EnabledFor.First().Name);
        } else {
            Assert.Single(definition.Variants);
            Assert.Null(definition.Variants.First().Name);
        }
    }
}

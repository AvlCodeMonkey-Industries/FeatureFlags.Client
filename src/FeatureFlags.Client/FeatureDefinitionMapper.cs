using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Client;

/// <summary>
/// Provides methods for converting custom feature definitions.
/// </summary>
public static class FeatureDefinitionMapper {
    /// <summary>
    /// Converts a <see cref="CustomFeatureDefinition"/> instance to a <see cref="FeatureDefinition"/> object.
    /// </summary>
    /// <param name="featureFlag">The <see cref="CustomFeatureDefinition"/> instance to convert. Cannot be <see langword="null"/>.</param>
    /// <returns>A new <see cref="FeatureDefinition"/> object containing the equivalent data from the specified <see cref="CustomFeatureDefinition"/>.</returns>
    public static FeatureDefinition ToFeatureDefinition(CustomFeatureDefinition featureFlag) => new() {
        Name = featureFlag.Name,
        EnabledFor = featureFlag.EnabledFor?.Select(x => {
            var parameters = new ConfigurationBuilder()
                .AddInMemoryCollection(x.Parameters)
                .Build();
            return new FeatureFilterConfiguration { Name = x.Name, Parameters = parameters };
        }),
        RequirementType = featureFlag.RequirementType,
        Status = featureFlag.Status,
        Allocation = featureFlag.Allocation,
        Variants = featureFlag.Variants ?? [],
        Telemetry = featureFlag.Telemetry
    };
}

using Microsoft.FeatureManagement;

namespace FeatureFlags.Client;

/// <summary>
/// Represents the definition of a feature, including its name, status, filters, and configuration details.
/// </summary>
/// <remarks>This will closely match FeatureDefinition. Needed because FeatureDefinition can't be deserialized.</remarks>
public sealed class CustomFeatureDefinition {
    /// <summary>
    /// Name of the feature.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the collection of feature filters that determine the conditions under which the feature is enabled.
    /// </summary>
    public IEnumerable<CustomFeatureFilterConfiguration> EnabledFor { get; set; } = new List<CustomFeatureFilterConfiguration>();

    /// <summary>
    /// Gets or sets the requirement type that determines whether any or all registered feature filters  must be enabled
    /// for the feature to be considered enabled.
    /// </summary>
    public RequirementType RequirementType { get; set; }

    /// <summary>
    /// Gets or sets the status of the feature, determining whether it is always disabled or conditionally enabled.
    /// </summary>
    public FeatureStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the allocation strategy for variants.
    /// </summary>
    public Allocation? Allocation { get; set; }

    /// <summary>
    /// Gets or sets a collection of variant definitions that specify configurations to return when assigned.
    /// </summary>
    public IEnumerable<VariantDefinition> Variants { get; set; } = Enumerable.Empty<VariantDefinition>();

    /// <summary>
    /// Gets or sets the telemetry-related configuration for the feature.
    /// </summary>
    /// <remarks>Use this property to configure telemetry options such as logging, monitoring, or  diagnostics
    /// for the feature. If set to <see langword="null"/>, telemetry will not be  configured for the feature.</remarks>
    public TelemetryConfiguration? Telemetry { get; set; }
}

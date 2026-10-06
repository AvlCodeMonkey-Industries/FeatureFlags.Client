namespace Acmi.FeatureFlags.Client;

/// <summary>
/// Magic strings used throughout the feature flags client.
/// </summary>
public static class Constants {
    /// <summary>
    /// Name of the HTTP client used for feature flag requests.
    /// </summary>
    public const string HttpClientName = "FeatureFlagHttpClient";

    /// <summary>
    /// Path, relative to the API base endpoint, that serves feature flags in the Microsoft Feature Management schema.
    /// </summary>
    public const string FeaturesPath = "features";

    /// <summary>
    /// Represents the name of the HTTP header used for API key authentication.
    /// </summary>
    public const string ApiKeyHeaderName = "x-api-key";
}

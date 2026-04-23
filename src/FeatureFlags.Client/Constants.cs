namespace FeatureFlags.Client;

/// <summary>
/// Magic strings used throughout the feature flags client.
/// </summary>
public static class Constants {
    /// <summary>
    /// Name of the HTTP client used for feature flag requests.
    /// </summary>
    public const string HttpClientName = "FeatureFlagHttpClient";

    /// <summary>
    /// Name of the authentication scheme used for feature flag requests.
    /// </summary>
    public const string Bearer = "Bearer";

    /// <summary>
    /// Represents the cache key used for storing feature definitions.
    /// </summary>
    public const string FeatureDefinitionsCacheKey = "FeatureDefinitionsCache";

    /// <summary>
    /// Represents the name of the HTTP header used for API key authentication.
    /// </summary>
    public const string ApiKeyHeaderName = "x-api-key";

    /// <summary>
    /// Represents the HTTP header name used to identify the originating IP address of a client connecting to a web
    /// server through an HTTP proxy or load balancer.
    /// </summary>
    public const string XForwardedForHeaderName = "x-forwarded-for";
}

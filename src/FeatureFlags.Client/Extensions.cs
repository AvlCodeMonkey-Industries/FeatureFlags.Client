using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

namespace FeatureFlags.Client;

/// <summary>
/// Provides extension methods for configuring feature flags.
/// </summary>
public static class Extensions {
    /// <summary>
    /// Configures the application to use feature flags by registering the necessary services and HTTP client.
    /// </summary>
    /// <remarks>This method retrieves the feature flag API base endpoint and API key from the application's
    /// configuration (using the keys <c>FeatureFlags:ApiBaseEndpoint</c> and <c>FeatureFlags:ApiKey</c>, respectively).
    /// If either value is missing or invalid, an <see cref="ArgumentException"/> is thrown.  The method registers an
    /// HTTP client with the specified base address and authorization header, as well as the required services for
    /// feature flag management, including memory caching and scoped feature management services.</remarks>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> used to configure the application.</param>
    /// <returns>The <see cref="IHostApplicationBuilder"/> instance, allowing for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if configuration value for <c>FeatureFlags:ApiBaseEndpoint</c> or <c>FeatureFlags:ApiKey</c> is null, empty, or whitespace.</exception>
    public static IHostApplicationBuilder AddFeatureFlags(this IHostApplicationBuilder builder) {
        var apiBaseEndpoint = builder.Configuration.GetValue<string>("FeatureFlags:ApiBaseEndpoint");
        if (string.IsNullOrWhiteSpace(apiBaseEndpoint)) {
            throw new ArgumentException("FeatureFlags:ApiBaseEndpoint is not configured.");
        }
        var apiKey = builder.Configuration.GetValue<string>("FeatureFlags:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) {
            throw new ArgumentException("FeatureFlags:ApiKey is not configured.");
        }

        // Register the feature flag client
        builder.Services.AddHttpClient(Constants.HttpClientName, client => {
            // Set the base address of the named client.
            client.BaseAddress = new Uri(apiBaseEndpoint);
            // Add the api key header for authentication.
            client.DefaultRequestHeaders.Add(Constants.ApiKeyHeaderName, apiKey);
        });

        // Register the feature management services
        builder.Services
            .AddMemoryCache()
            .AddScoped<IFeatureFlagClient, HttpFeatureFlagClient>()
            .AddScoped<IFeatureDefinitionProvider, ClientFeatureDefinitionProvider>()
            .AddScopedFeatureManagement()
            .AddFeatureFilter<ConsistentPercentageFilter>()
            .WithTargeting();

        return builder;
    }
}

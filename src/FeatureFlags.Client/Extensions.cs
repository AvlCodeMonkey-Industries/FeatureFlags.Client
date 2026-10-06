using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

namespace Acmi.FeatureFlags.Client;

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
    /// feature flag management, including a background service that refreshes definitions and scoped feature management services.</remarks>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> used to configure the application.</param>
    /// <returns>The <see cref="IHostApplicationBuilder"/> instance, allowing for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if required configuration is missing or the cache expiration interval is invalid.</exception>
    public static IHostApplicationBuilder AddFeatureFlags(this IHostApplicationBuilder builder) {
        var apiBaseEndpoint = builder.Configuration.GetValue<string>("FeatureFlags:ApiBaseEndpoint");
        if (string.IsNullOrWhiteSpace(apiBaseEndpoint)) {
            throw new ArgumentException("FeatureFlags:ApiBaseEndpoint is not configured.");
        }
        var apiKey = builder.Configuration.GetValue<string>("FeatureFlags:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) {
            throw new ArgumentException("FeatureFlags:ApiKey is not configured.");
        }
        ValidateRefreshInterval(builder.Configuration);

        // Register the feature flag client
        builder.Services.AddHttpClient(Constants.HttpClientName, client => {
            // Set the base address of the named client.
            client.BaseAddress = new Uri(apiBaseEndpoint);
            // Add the api key header for authentication.
            client.DefaultRequestHeaders.Add(Constants.ApiKeyHeaderName, apiKey);
        });

        // Register the feature management services
        builder.Services
            .AddSingleton<FeatureDefinitionRefreshService>()
            .AddHostedService(sp => sp.GetRequiredService<FeatureDefinitionRefreshService>())
            .AddScoped<IFeatureFlagClient, HttpFeatureFlagClient>()
            .AddScoped<IFeatureDefinitionProvider, ClientFeatureDefinitionProvider>()
            .AddScopedFeatureManagement()
            .WithTargeting();

        return builder;
    }

    private static void ValidateRefreshInterval(IConfiguration configuration) {
        const string key = "FeatureFlags:CacheExpirationInMinutes";
        var value = configuration[key];
        if (value is null) {
            return;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
            || !double.IsFinite(minutes)
            || minutes <= 0
            || minutes > int.MaxValue / (double)TimeSpan.MillisecondsPerMinute
            || TimeSpan.FromMinutes(minutes).TotalMilliseconds > int.MaxValue) {
            throw new ArgumentException(
                $"Configuration value '{key}' must be a positive number that fits within the supported refresh timeout.");
        }
    }
}

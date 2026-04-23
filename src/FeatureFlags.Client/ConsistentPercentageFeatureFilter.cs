using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace FeatureFlags.Client;

/// <summary>
/// A feature filter that can be used to activate a feature based on a random percentage.
/// </summary>
/// <remarks>
/// Creates a percentage based feature filter that consistently evaluates to the same value for a user.
/// </remarks>
/// <param name="httpContextAccessor">HTTP context accessor for accessing the current HTTP context.</param>
/// <param name="loggerFactory">A logger factory for creating loggers.</param>
[FilterAlias(_Alias)]
public class ConsistentPercentageFilter(IHttpContextAccessor httpContextAccessor, ILoggerFactory? loggerFactory = null) : IFeatureFilter, IFilterParametersBinder {
    private const string _Alias = "FeatureFlags.ConsistentPercentage";
    private readonly IHttpContextAccessor _HttpContextAccessor = httpContextAccessor;
    private readonly ILogger? _Logger = loggerFactory?.CreateLogger<ConsistentPercentageFilter>();

    /// <summary>
    /// Binds configuration representing filter parameters to <see cref="PercentageFilterSettings"/>.
    /// </summary>
    /// <param name="parameters">Configuration of filter that should be bound to <see cref="PercentageFilterSettings"/>.</param>
    /// <returns><see cref="PercentageFilterSettings"/> that can later be used in feature evaluation.</returns>
    public object BindParameters(IConfiguration parameters) => parameters.Get<PercentageFilterSettings>() ?? new PercentageFilterSettings();

    /// <summary>
    /// Performs a percentage based evaluation to determine whether a feature is enabled.
    /// </summary>
    /// <param name="context">The feature evaluation context.</param>
    /// <returns>True if the feature is enabled, false otherwise.</returns>
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context) {
        ArgumentNullException.ThrowIfNull(context);

        // Check if prebound settings available, otherwise bind from parameters.
        var settings = (PercentageFilterSettings)context.Settings ?? (PercentageFilterSettings)BindParameters(context.Parameters);

        var result = true;

        if (settings.Value < 0) {
            _Logger?.LogWarning(
                "The '{Alias}' feature filter does not have a valid '{ValueProperty}' value for feature '{FeatureName}'",
                _Alias,
                nameof(settings.Value),
                context.FeatureName
            );

            result = false;
        }

        if (result) {
            var name = _HttpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
            if (string.IsNullOrEmpty(name)) {
                // can't identify the user so generate a random value
                result = (RandomGenerator.NextDouble() * 100) < settings.Value;
            } else {
                // convert name to an integer by summing ASCII values of its characters,
                // then map into 0..99 via modulo to produce a consistent percentage
                long sum = 0;
                foreach (var ch in name) {
                    sum += ch;
                }

                var percent = (int)(sum % 100);
                result = percent < settings.Value;
            }
        }

        return Task.FromResult(result);
    }
}

using Microsoft.FeatureManagement;

namespace Acmi.FeatureFlags.Client;

/// <summary>
/// Interface for a client that retrieves feature flag definitions from a remote service.
/// </summary>
public interface IFeatureFlagClient {
    /// <summary>
    /// Gets all feature definitions from the current in-memory snapshot. Never waits on HTTP.
    /// Returns an empty list if no refresh has succeeded yet.
    /// </summary>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> that can be used to cancel the operation. Default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns></returns>
    Task<List<FeatureDefinition>> GetAllFeatureDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a feature definition by its name from the current in-memory snapshot. Never waits on HTTP.
    /// </summary>
    /// <param name="name">Name of feature.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> that can be used to cancel the operation. Default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>FeatureDefinition is found, else null.</returns>
    Task<FeatureDefinition?> GetFeatureDefinitionByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a refresh of feature definitions from the remote service right now, without waiting for the next interval.
    /// The refresh runs in the background. The existing snapshot stays in place until it succeeds,
    /// so if the service is unreachable the previous definitions keep being used.
    /// </summary>
    /// <returns>True once the refresh has been requested.</returns>
    bool ClearCache();
}

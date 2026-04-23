using Microsoft.FeatureManagement;

namespace FeatureFlags.Client;

/// <summary>
/// Interface for a client that retrieves feature flag definitions from a remote service.
/// </summary>
public interface IFeatureFlagClient {
    /// <summary>
    /// Gets all feature definitions from the remote service.
    /// </summary>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> that can be used to cancel the operation. Default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns></returns>
    Task<List<FeatureDefinition>> GetAllFeatureDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a feature definition by its name from the remote service.
    /// </summary>
    /// <param name="name">Name of feature.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> that can be used to cancel the operation. Default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>FeatureDefinition is found, else null.</returns>
    Task<FeatureDefinition?> GetFeatureDefinitionByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the cache of feature definitions.
    /// </summary>
    /// <returns>True if successful, else false.</returns>
    bool ClearCache();
}

using Microsoft.FeatureManagement;

namespace FeatureFlags.Client;

/// <summary>
/// Provides feature definitions by interacting with a feature flag client.
/// </summary>
/// <remarks>This class acts as a bridge between the feature flag client and the feature management system,
/// enabling retrieval of feature definitions either individually or as a collection.</remarks>
/// <param name="featureFlagClient">The feature flag client used to retrieve feature definitions.</param>
public class ClientFeatureDefinitionProvider(IFeatureFlagClient featureFlagClient) : IFeatureDefinitionProvider {
    private readonly IFeatureFlagClient _FeatureFlagClient = featureFlagClient;

    /// <summary>
    /// Retrieves all feature definitions asynchronously as an asynchronous stream.
    /// </summary>
    /// <remarks>This method returns an <see cref="IAsyncEnumerable{T}"/> of <see cref="FeatureDefinition"/>, 
    /// allowing the caller to enumerate feature definitions as they are retrieved.  The enumeration is performed
    /// lazily, and the caller can process each feature definition  without waiting for the entire collection to be
    /// loaded.</remarks>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="FeatureDefinition"/> representing all feature definitions.</returns>
    public async IAsyncEnumerable<FeatureDefinition> GetAllFeatureDefinitionsAsync() {
        var featureFlags = await _FeatureFlagClient.GetAllFeatureDefinitionsAsync();
        foreach (var featureDefinition in featureFlags) {
            yield return featureDefinition;
        }
    }

    /// <summary>
    /// Retrieves the definition of a feature by its name asynchronously.
    /// </summary>
    /// <remarks>This method queries the feature flag client to retrieve the definition of the specified
    /// feature. Ensure that the feature name provided is valid and corresponds to an existing feature.</remarks>
    /// <param name="featureName">The name of the feature to retrieve. Cannot be null or empty.</param>
    /// <returns>A <see cref="FeatureDefinition"/> object representing the feature's definition if found, else an empty one is created</returns>
    public async Task<FeatureDefinition> GetFeatureDefinitionAsync(string featureName)
        => await _FeatureFlagClient.GetFeatureDefinitionByNameAsync(featureName) ?? new FeatureDefinition { Name = featureName };
}

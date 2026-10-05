using Microsoft.FeatureManagement;

namespace Acmi.FeatureFlags.Client;

/// <inheritdoc />
/// <remarks>
/// Reads from the in-memory snapshot maintained by <see cref="FeatureDefinitionRefreshService"/>; it never makes an HTTP call itself.
/// </remarks>
public class HttpFeatureFlagClient(FeatureDefinitionRefreshService refreshService) : IFeatureFlagClient {
    private readonly FeatureDefinitionRefreshService _RefreshService = refreshService;

    /// <inheritdoc />
    public Task<List<FeatureDefinition>> GetAllFeatureDefinitionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_RefreshService.GetDefinitions().ToList());

    /// <inheritdoc />
    public Task<FeatureDefinition?> GetFeatureDefinitionByNameAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_RefreshService.GetDefinition(name));

    /// <inheritdoc />
    public bool ClearCache() {
        _RefreshService.RequestRefresh();
        return true;
    }
}

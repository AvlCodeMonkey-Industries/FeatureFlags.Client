using System.Diagnostics;
using FeatureFlags.Client;
using FeatureFlags.Demo.Models;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlags.Demo.Controllers;

public class HomeController(IFeatureFlagClient featureFlagClient) : Controller {
    private readonly IFeatureFlagClient _FeatureFlagClient = featureFlagClient;

    public IActionResult Index() => View();

    public IActionResult ClearCache() {
        // reset memory cache
        _FeatureFlagClient.ClearCache();
        return View("Index");
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

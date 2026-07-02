# FeatureFlags.app

Deploy anytime. Release when you're ready. FeatureFlags.app gives .NET developers cloud-agnostic feature management with no user tracking, no vendor lock-in, and seamless Microsoft integration. Built for Modern .NET Applications, it's the perfect solution for development teams seeking a simple, no-frills feature flag management system.

Get started at https://featureflags.app, or if you want details first and vibes later, keep reading.

## Contents

- [What This Library Does](#what-this-library-does)
- [Package And Runtime](#package-and-runtime)
- [Quick Start](#quick-start)
- [Using Flags In Code](#using-flags-in-code)
- [Cache Behavior](#cache-behavior)
- [Filter Support](#filter-support)
- [Failure Semantics](#failure-semantics)
- [Common Issues And Fixes](#common-issues-and-fixes)
- [Local Validation](#local-validation)
- [Related References](#related-references)
- [Support And Issue Tracking](#support-and-issue-tracking)

## What This Library Does

- Registers feature management services in ASP.NET Core via a single `AddFeatureFlags()` call.
- Fetches feature definitions from our API using an API key header (`x-api-key`).
- Exposes `IFeatureManager`/`IFeatureManagerSnapshot` usage patterns you already know from `Microsoft.FeatureManagement`.
- Includes a deterministic percentage filter (`Acmi.FeatureFlags.ConsistentPercentage`) and targeting support.

## Package And Runtime

- Package: [`Acmi.FeatureFlags.Client`](https://www.nuget.org/packages/Acmi.FeatureFlags.Client/)
- Namespace: `Acmi.FeatureFlags.Client`
- Target framework: `net10.0`
- Core dependency: `Microsoft.FeatureManagement.AspNetCore`

## Quick Start

### 1. Install package

```bash
dotnet add package Acmi.FeatureFlags.Client
```

### 2. Register services in `Program.cs`

```csharp
using Acmi.FeatureFlags.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.AddFeatureFlags();

var app = builder.Build();
// ... middleware and endpoints ...
await app.RunAsync();
```

### 3. Configure endpoint + API key

```json
{
  "FeatureFlags": {
	"ApiBaseEndpoint": "https://featureflags.app/api/",
	"ApiKey": "[from secrets]",
	"CacheExpirationInMinutes": 15
  }
}
```

Required keys:

- `FeatureFlags:ApiBaseEndpoint`
- `FeatureFlags:ApiKey`

Optional key:

- `FeatureFlags:CacheExpirationInMinutes` (default: `15`)

## Using Flags In Code

Use `IFeatureManagerSnapshot` exactly like standard Microsoft feature management.

```csharp
using Microsoft.FeatureManagement;

public class ProductController : Controller {
	private readonly IFeatureManagerSnapshot _featureManager;

	public ProductController(IFeatureManagerSnapshot featureManager) {
		_featureManager = featureManager;
	}

	public async Task<IActionResult> Index() {
		if (await _featureManager.IsEnabledAsync("NewProductPage")) {
			return View("NewProductPage");
		}

		return View("ProductPage");
	}
}
```

Also works with the normal ASP.NET Core feature management integrations:

- `FeatureGate` attributes
- Razor feature tag helpers
- Filter-based evaluations from feature definitions

## Cache Behavior

`HttpFeatureFlagClient` caches all retrieved definitions in memory under a single cache entry.

- First request fetches from remote API.
- Subsequent requests read from cache until expiration.
- Expiration defaults to 15 minutes.
- You can evict cache manually via `IFeatureFlagClient.ClearCache()`.

Example:

```csharp
public class AdminController : Controller {
	private readonly IFeatureFlagClient _featureFlagClient;

	public AdminController(IFeatureFlagClient featureFlagClient) {
		_featureFlagClient = featureFlagClient;
	}

	[HttpPost]
	public IActionResult RefreshFlags() {
		_featureFlagClient.ClearCache();
		return Ok(new { message = "Feature flag cache cleared." });
	}
}
```

## Filter Support

Service registration wires up:

- `AddScopedFeatureManagement()`
- `.AddFeatureFilter<ConsistentPercentageFilter>()`
- `.WithTargeting()`

### Consistent percentage filter

Alias: `FeatureFlags.ConsistentPercentage`

Behavior:

- If user identity name exists, result is deterministic per user.
- If user identity name is missing, it falls back to random evaluation.
- If configured percentage value is invalid (`< 0`), evaluation returns `false`.

Use this filter when you want stable rollout behavior for authenticated users instead of request-by-request randomness.

## Failure Semantics

When remote API calls fail:

- Client logs an error.
- `GetAllFeatureDefinitionsAsync()` returns an empty list.
- `GetFeatureDefinitionByNameAsync()` returns `null`.

In practice this means feature checks degrade to "off" unless your app defines alternate behavior. This is generally safer than throwing exceptions into request pipelines and setting your pager on fire.

## Common Issues And Fixes

### 1. `AddFeatureFlags()` throws at startup

Symptoms:

- `FeatureFlags:ApiBaseEndpoint is not configured.`
- `FeatureFlags:ApiKey is not configured.`

Fix:

- Add both required keys to configuration.
- Confirm environment-specific config is loaded (`appsettings.{Environment}.json`, user secrets, env vars).

### 2. Flags always evaluate to false

Possible causes:

- API key invalid or missing permissions.
- API unavailable (client degrades to empty definitions).
- Flag name mismatch (`"NewDashboard"` vs `"NewDashbaord"`, yes this typo happens a lot).

Fix:

- Verify API key and endpoint.
- Check app logs for "Error fetching feature definitions".
- Centralize flag names in constants to avoid string-literal drift.

### 3. Rollout percentages look random per request

Cause:

- User identity name is missing, so consistent percentage filter uses random fallback.

Fix:

- Ensure authenticated identity with stable `User.Identity.Name`.
- If anonymous traffic dominates, choose filter strategy accordingly.

### 4. Flag updates not visible immediately

Cause:

- Cached definitions not yet expired.

Fix:

- Lower `CacheExpirationInMinutes` for development.
- Call `IFeatureFlagClient.ClearCache()` after admin updates when immediate refresh is required.

## Local Validation

This repository includes:

- `src/FeatureFlags.Client` (library)
- `src/FeatureFlags.Client.Tests` (unit tests)
- `src/FeatureFlags.Demo` (demo MVC app)

Run tests:

```bash
dotnet test src/FeatureFlags.Client.Tests/FeatureFlags.Client.Tests.csproj
```

Run demo:

```bash
dotnet run --project src/FeatureFlags.Demo/FeatureFlags.Demo.csproj
```

## Related References

- Hosted app and docs entry point: https://featureflags.app
- Microsoft feature management overview: https://learn.microsoft.com/azure/azure-app-configuration/feature-management-overview
- ASP.NET Core quickstart concepts: https://learn.microsoft.com/azure/azure-app-configuration/quickstart-feature-flag-aspnet-core

## Support And Issue Tracking

If something breaks or behaves strangely:

- Browse/search issues: https://github.com/avlcodemonkey-industries/FeatureFlags.Client/issues
- Open a new issue: https://github.com/avlcodemonkey-industries/FeatureFlags.Client/issues/new

Include:

- .NET version
- Package version
- Sanitized `FeatureFlags` configuration
- Relevant logs/exceptions
- Repro steps

That gives maintainers a chance to help quickly instead of reenacting a detective novel.

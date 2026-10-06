# FeatureFlags.app

Deploy anytime. Release when you're ready. FeatureFlags.app gives .NET developers cloud-agnostic feature management with no user tracking, no vendor lock-in, and seamless Microsoft integration. Built for Modern .NET Applications, it's the perfect solution for development teams seeking a simple, no-frills feature flag management system. FeatureFlags.Client is the client library that integrates your application with FeatureFlags.app.

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
- Fetches feature definitions from our API using an API key header (`x-api-key`), in a background service, and keeps the last-known-good copy if a refresh fails. The API serves the standard Microsoft Feature Management schema (`feature_management.feature_flags[]`), parsed by Microsoft's own provider.
- Exposes `IFeatureManager`/`IFeatureManagerSnapshot` usage patterns you already know from `Microsoft.FeatureManagement`.
- Supports Microsoft's built-in filters, including targeting. Percentage rollouts use the Targeting filter, so users are placed in or out of a rollout consistently.

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

`AddFeatureFlags()` registers a hosted background service (`FeatureDefinitionRefreshService`) that keeps an in-memory snapshot of all feature definitions.

- Definitions are fetched at startup. Startup waits up to 5 seconds for the first fetch, then continues without it.
- After each refresh completes, the next periodic refresh starts after `CacheExpirationInMinutes` (default: `15`); requests time out after 30 seconds.
- Flag checks read the current snapshot. Evaluation never waits on an HTTP call.
- The snapshot is swapped atomically after each successful refresh.
- If a refresh fails (timeout, network error, 5xx, 401/403), the last-known-good snapshot stays in place and the refresh is retried with exponential backoff (see "Failure Semantics").
- `IFeatureFlagClient.ClearCache()` requests an immediate background refresh. The old snapshot stays in place until that refresh succeeds. The method name is kept for compatibility.
- Flag changes typically reach your app within the configured interval plus the time taken by the next refresh (up to 30 seconds), assuming the API responds successfully. Use a shorter interval for apps that rely on kill-switch flags.

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
		return Accepted(new { message = "Feature flag refresh requested." });
	}
}
```

## Filter Support

Service registration wires up:

- `AddScopedFeatureManagement()`
- `.WithTargeting()`

Flags use Microsoft's built-in `Microsoft.Targeting` and `Microsoft.TimeWindow` filters. Custom filters defined in the dashboard receive their `parameters` under their own names (for example `Level`), exactly as in a plain `appsettings.json`.

### Percentage rollouts

A percentage flag is served as Microsoft's Targeting filter with a `DefaultRolloutPercentage`. Microsoft's filter hashes the user id together with the feature name, so:

- Each user stays in or out of the rollout on every request, and the rollout size matches the configured percentage.
- Different flags bucket the same user differently, so one rollout doesn't always pick the same users.
- The user id comes from `User.Identity.Name` by default (`.WithTargeting()`). Without an identity, every anonymous request lands in the same bucket, so a percentage flag is either on or off for all anonymous traffic. To roll out to anonymous visitors, register your own `ITargetingContextAccessor` that supplies a stable id such as a cookie or session id.

## Failure Semantics

When a refresh fails:

- The client logs a warning with the reason. During a long outage it logs the first failure and then at most once an hour.
- The last-known-good definitions keep being served, and the refresh is retried with exponential backoff (2, 4, 8 seconds and so on, capped at the normal interval) rather than waiting out the full interval. Refreshes are never closer than 5 seconds apart, even if `ClearCache()` is called repeatedly.
- A log message is written when refreshes recover.

**Cold start with the API down:** if the app starts while FeatureFlags.app is unreachable (or the API key is invalid), no snapshot exists yet. `GetAllFeatureDefinitionsAsync()` returns an empty list, `GetFeatureDefinitionByNameAsync()` returns `null`, and all flags evaluate off until the first successful fetch. Nothing is thrown into the request pipeline.

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
- The API was unreachable the whole time since the app started, so no definitions have been loaded (see "Cold start" above).
- Flag name mismatch (`"NewDashboard"` vs `"NewDashbaord"`, yes this typo happens a lot).

Fix:

- Verify API key and endpoint.
- Check app logs for "Failed to refresh feature definitions".
- Centralize flag names in constants to avoid string-literal drift.

### 3. A percentage rollout is all on or all off for anonymous users

Cause:

- There is no user identity, so every anonymous request is placed in the same Targeting bucket.

Fix:

- Ensure authenticated users have a stable `User.Identity.Name`.
- For anonymous traffic, register an `ITargetingContextAccessor` that supplies a stable id (cookie or session id).

### 4. Flag updates are not visible right away

Cause:

- After each refresh completes, the next periodic refresh starts after `CacheExpirationInMinutes` (15 by default). A change may take that interval plus the time taken by the next refresh (up to 30 seconds) to show up.

Fix:

- Lower `CacheExpirationInMinutes` for development, or for apps that rely on kill-switch flags.
- Call `IFeatureFlagClient.ClearCache()` to request an immediate background refresh. It returns right away, and the new values appear once the refresh completes.

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

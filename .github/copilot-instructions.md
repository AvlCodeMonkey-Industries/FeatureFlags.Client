# GitHub Copilot Instructions

## Project Overview
This repository is a .NET 10 solution centered on the feature flag client library. It includes:
- Client library for retrieving remote feature flag definitions and integrating with Microsoft Feature Management.
- Demo MVC application for manually testing and validating client-library behavior.
- Unit tests for the client library.

## Coding Style
- Use C# 14 features and idioms.
- Prefer async/await for asynchronous operations.
- Use dependency injection for all services and clients.
- Follow conventions from the editorconfig file.
- Use records for immutable models where appropriate.
- Use sealed classes where inheritance is not needed.
- Favor simple, expressive code over complex constructs.
- Keep performance in mind, but prioritize readability and maintainability.
- Keep code comments concise and relevant. Don't start comments with an article like "a", "an", or "the".

## MVC Guidance
- Prioritize MVC patterns over Blazor or Razor Pages in the demo application.
- Keep the demo focused on exercising and validating client-library behavior rather than adding product-style features.

## Testing
- Use xUnit for unit tests.
- Use Moq for mocking dependencies.
- Test both success and error paths for client services, extension methods, filters, and demo behaviors when relevant.
- Place tests in the corresponding `*.Tests` projects.

## Feature Flags
- The client library (`IFeatureFlagClient`, `HttpFeatureFlagClient`) fetches definitions from a remote API.
- The client library should remain reusable and independent from application-specific domain logic.
- The demo project exists to make local verification and manual testing easier.

## Extensibility
- Use extension methods for reusable logic.
- Prefer adding new client-facing capabilities to the library first, then expose them in the demo only when needed for testing or examples.

## General Guidelines
- Keep demo controllers thin; delegate reusable behavior to the client library.
- Document public APIs with XML comments.
- Use cancellation tokens for async service methods.
- Don't upgrade the .NET SDK settings (in global.json), Nuget packages, or NPM packages.  A human or dependabot will handle those updates.

## Contribution
- Follow the existing folder and namespace structure.
- Write unit tests for all new features.
- Ensure the client library, demo project, and tests build and pass on .NET 10.

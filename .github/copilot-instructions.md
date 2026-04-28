# Copilot Instructions

## Coding Style
- Use C# 14 features and idioms.
- Follow conventions from the editorconfig file.
- Use records for immutable models where appropriate.
- Use sealed classes where inheritance is not needed.
- Favor simple, expressive code over complex constructs.
- Keep performance in mind, but prioritize readability and maintainability.
- Keep code comments concise and relevant. Don't start comments with an article like "a", "an", or "the".

## Feature Flags
- The client library (`IFeatureFlagClient`, `HttpFeatureFlagClient`) fetches definitions from a remote API.
- The client library should remain reusable and independent from application-specific domain logic.
- The demo project exists to make local verification and manual testing easier.

## General Guidelines
- Don't upgrade the .NET SDK settings (in global.json), Nuget packages, or NPM packages.  A human or dependabot will handle those updates.

## Contribution
- Follow the existing folder and namespace structure.
- Write unit tests for all new features.
- Ensure the client library, demo project, and tests build and pass on .NET 10.

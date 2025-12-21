# Design rules and conventions

## Project structure
- `src/<AssemblyName>/` contains the solution and projects for that assembly.
- `tests/<AssemblyName>.Tests/` contains unit tests for the assembly.

## Solutions and harnesses
- Each assembly should have a single solution file (`<AssemblyName>.sln`).
- Add a small DevHarness console app in the same solution for manual testing and demos.

## Build and CI
- Use `Directory.Build.props` at the repo root for shared settings.
- CI should run `dotnet restore`, `dotnet build`, and `dotnet test` on PRs and pushes to `main`.

## Secrets
- Local dev: `dotnet user-secrets`.
- CI: GitHub Actions Secrets.
- Production/staging: Azure Key Vault.

## Public API
- Add Public API enforcement later when the library API stabilizes.

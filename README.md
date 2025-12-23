# MetWorks

Monorepo for MetWorks projects. Each assembly lives under `src/<AssemblyName>` and tests live under `tests/<AssemblyName.Tests>`.

## Quickstart

1. Clone the repo:
   ```bash
   git clone https://github.com/jack-metcalfe/MetWorks.git
   cd MetWorks
   ```

2. Restore packages for the main solution:
   ```bash
   dotnet restore metworks-ddi-gen.sln
   ```

3. Build the solution:
   ```bash
   dotnet build metworks-ddi-gen.sln --configuration Release
   ```

4. Run tests:
   ```bash
   dotnet test metworks-ddi-gen.sln --no-build
   ```

## Repository Layout

- `src/` — project assemblies and solutions (one folder per assembly)
- `tests/` — unit and integration tests
- `.github/workflows/` — CI configurations (GitHub Actions)
- `docs/` — design notes and conventions
- `Directory.Build.props` — shared build settings

## Conventions

- One Visual Studio project per assembly under `src/<AssemblyName>/`
- Keep implementation projects under `src/<AssemblyName>/` and tests under `tests/<AssemblyName.Tests>/`
- Use `dotnet user-secrets` for local development; use a secret manager for staging/production

## CI

GitHub Actions runs restore, build, and tests on pushes and pull requests to main. See `.github/workflows/` for details.

## Local Development Tips

- If you use WSL, install the matching .NET SDK inside WSL (dotnet-sdk-8.0) to run CLI commands
- If you use Visual Studio on Windows, open the solution from the repo root
- To clean bins and obj artifacts locally:
  ```bash
  git clean -ndX   # preview
  git clean -fdX   # remove files (destructive)
  ```

## Testing GitHub Actions Locally

This repo supports testing Actions locally with [act](https://github.com/nektos/act):

1. Copy `.act.env.example` to `.act.env`
2. Add your GitHub personal access token to `.act.env`
3. Run: `act -j <job-name>`

## Developer Quick Start

- **Open the solution**: Use `metworks-ddi-gen.sln` in the repo root
- **Restore, build, test**:
  ```bash
  dotnet restore metworks-ddi-gen.sln
  dotnet build metworks-ddi-gen.sln --configuration Release
  dotnet test metworks-ddi-gen.sln --no-build
  ```

## Run Diagnostics Validation

```bash
export GITHUB_WORKSPACE="$(pwd)"
SOLUTION=metworks-ddi-gen.sln bash .github/scripts/validate-diagnostics.sh
```

## Architectural Principles

**Canonical DTO project**: All shared data transfer objects live in `DdiCodeGen.SourceDto`. This avoids duplication and ensures a single source of truth for contracts.

**Minimal class libraries**: New libraries are created with the `met-classlib` template. They start clean — no implicit references, no bundled DTOs.

**Opt-in references**: Dependencies are added explicitly by developers using `dotnet add reference` or the provided helper scripts. Nothing is wired automatically, keeping assemblies small and intentional.

**Consistency**: All `.csproj` files use lowercase booleans (`true`/`false`) and explicit language/version settings for clarity.

**Auditability**: Every decision (symbols, references, defaults) is documented in the template and solution README for future contributors.

## Workflow for Contributors

### Create a New Library

```bash
dotnet new met-classlib -n MyLib -o src/MyLib
```

This generates a minimal `.csproj` with defaults (`net8.0`, `false`, `enable`, `latest`).

### Add References Manually

```bash
dotnet add src/MyLib/MyLib.csproj reference src/DdiCodeGen.SourceDto/DdiCodeGen.SourceDto.csproj
```

Or use the helper scripts (`add-references.sh` / `add-references.cmd`) for convenience.

### Validate with CI

CI builds both a plain instantiation and a reference-enabled instantiation. This ensures templates remain DRY and reproducible.

### Document Rationale

Any new library should include a short README explaining its purpose and dependencies. This keeps onboarding smooth and avoids hidden coupling.
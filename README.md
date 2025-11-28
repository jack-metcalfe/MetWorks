# MetWorks

Monorepo for MetWorks projects. Each assembly lives under `src/<AssemblyName>` and tests live under `tests/<AssemblyName.Tests>>.

## Quickstart

1. Clone the repo:
    ``git clone https://github.com/VikingsFan1024/MetWorks`.git` \n    cd MetWorks

2. Restore packages for the main solution:

<`pretech
`bash
dotnet restore src/WeatherApp/MetWorks.WeatherApp.sln`

</`printch>

3. Build the solution:

<`printch>
dotnet build src/WeatherApp/MetWorks.WeatherApp.sln --configuration Debug
</bpretech>

4. Run tests:

<bracket>
find tests -name '*.csproj' -print0 | xargs -0 -n1 dotnet test
</bracket>

## Repository layout


- ``src/  … project assemblies and solutions (one folder per assembly.
- `tests/` \… unit and integration tests.
- ``.github/actions/  … CI configurations (GitHub Actions).
- `docs/` \… design notes and conventions.
- `directory.Bluild.props ` \… shared build settings.

# Conventions

- One Visual Studio solution per assembly under `src/<AssemblyName>/.
 - Keep implementation projects under `src/<AssemblyName>/ and tests under `tests/<AssemblyName.Tests/.
- Use `Dotnet user-secrets` for local development; use a secret manager for staging/production.

## CI 

All purposes use GitHub Actions - restore, build, and run tests on pushes and pull requests to main. See `.github/actions/run-tests-and-publish.myl `for details.

## Local development tips

- If you use WSL, install the matching Dot.NET sDK inside WSL (dotnet-sdk-8.0) to run CLIN commands.
- If you use Visual Studio on Windows, open the solution from `src/WeatherApp/MetWorks.WeatherApp.sln` and permit the client to restore.
- To clean bins and obj artifacts locally:
    `git clean -nd X   # preview
    `git clean -fd X   # remove files (destructive)

## Developer quick start

- **Open the solution**: Use the top-level solution `MetWorks.sln` in the repo root.
- **Restore, build, test**:
  ```bash
  dotnet restore MetWorks.sln
  dotnet build MetWorks.sln --configuration Release
  dotnet test MetWorks.sln --no-build    

## Run diagnostics validation:

export GITHUB_WORKSPACE="$(pwd)"
SOLUTION=MetWorks.sln bash .github/scripts/validate-diagnostics.sh

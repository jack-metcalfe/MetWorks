# Contributing Guidelines

## How to Contribute
1. Fork the repository
2. Create a feature branch
3. Commit your changes with clear messages
4. Open a pull request

## Coding Standards
- Follow the rules in `Directory.Build.props`
- Write tests for new features
- Document public APIs

## Local validation and line endings

**Run diagnostics validation locally**

You can run the repository diagnostics validation script locally before opening a PR:

```bash
# from repo root
export GITHUB_WORKSPACE="$(pwd)"
SOLUTION=MetWorks.sln bash .github/scripts/validate-diagnostics.sh

## Package management and local validation

**Central package versions**  
This repository uses `PackageReference` with central versions defined in `Directory.Packages.props`. Do not commit expanded NuGet packages or a `packages/` folder — CI will fail PRs that include vendored packages.

**Run validation locally**  
Before opening a PR, run the diagnostics validation and package audit:

```bash
# run diagnostics validation
export GITHUB_WORKSPACE="$(pwd)"
SOLUTION=MetWorks.sln bash .github/scripts/validate-diagnostics.sh

# run package audit (PowerShell Core required)
pwsh ./scripts/audit-packages.ps1

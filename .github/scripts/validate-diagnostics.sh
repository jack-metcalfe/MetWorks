#!/usr/bin/env bash
set -euo pipefail

# Defaults for local runs: use current directory if GITHUB_WORKSPACE is not set
: "${GITHUB_WORKSPACE:=$(pwd)}"
# Simple diagnostics validation script
# Usage:
#   SOLUTION=MetWorks.sln ./validate-diagnostics.sh
# In CI the workflow should run: bash .github/scripts/validate-diagnostics.sh
SOLUTION="${SOLUTION:-MetWorks.sln}"
CONFIGURATION="${CONFIGURATION:-Release}"
RESULTS_DIR="${RESULTS_DIR:-$GITHUB_WORKSPACE/artifacts/DiagnosticsResults}"

echo "Validate diagnostics: workspace=$GITHUB_WORKSPACE solution=$SOLUTION configuration=$CONFIGURATION"

echo "Validate diagnostics: solution=$SOLUTION configuration=$CONFIGURATION"

# Ensure dotnet is available
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet CLI not found in PATH" >&2
  exit 2
fi

# Restore
echo "Restoring $SOLUTION..."
dotnet restore "$SOLUTION"

# Build
echo "Building $SOLUTION (configuration=$CONFIGURATION)..."
dotnet build "$SOLUTION" --configuration "$CONFIGURATION" --no-restore

# Create results dir
mkdir -p "$RESULTS_DIR"

# Run diagnostics checks
# Replace or extend the commands below with your repo-specific validation logic.
# Examples:
#  - run a diagnostics project/unit tests
#  - run a custom analyzer or validation tool
#  - run a script that validates JSON/manifest files

# Example: run any test projects under tests that relate to diagnostics
echo "Running diagnostics-related tests (if any)..."
if [ -d tests ]; then
  find tests -name '*Diagnostics*.csproj' -print0 | xargs -0 -r -n1 bash -c '
    proj="$0"
    echo "Testing $proj"
    dotnet test "$proj" --no-build --configuration "'"$CONFIGURATION"'" --logger trx --results-directory "'"$RESULTS_DIR"'"
  '
else
  echo "No tests directory found; skipping diagnostics tests"
fi

# Example: run a repo-specific validation script or tool if present
if [ -x .github/scripts/diagnostics-checks-local.sh ]; then
  echo "Running repo-specific diagnostics checks script"
  .github/scripts/diagnostics-checks-local.sh || {
    echo "Repo-specific diagnostics checks failed" >&2
    exit 3
  }
else
  echo "No repo-specific diagnostics checks script found; skipping"
fi

# Summarize results
echo "Diagnostics validation completed. Results (if any) are in: $RESULTS_DIR"
ls -la "$RESULTS_DIR" || true

echo "All diagnostics checks passed"
exit 0

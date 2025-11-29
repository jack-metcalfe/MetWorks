#!/usr/bin/env bash
set -euo pipefail

ROOT="$(pwd)"
OUT="audit-report.md"
SOLUTION="MetWorks.sln"

echo "# Solution audit report" > "$OUT"
echo "" >> "$OUT"
echo "Generated: $(date -u)" >> "$OUT"
echo "" >> "$OUT"

echo "## Solution projects" >> "$OUT"
if command -v dotnet >/dev/null 2>&1; then
  dotnet sln "$SOLUTION" list >> "$OUT" 2>/dev/null || {
    echo "_dotnet could not list solution; ensure MetWorks.sln exists in repo root._" >> "$OUT"
  }
else
  echo "_dotnet CLI not found; install dotnet to run full analysis._" >> "$OUT"
fi
echo "" >> "$OUT"

echo "## Project summary" >> "$OUT"
echo "" >> "$OUT"
printf "| Project file | SDK / TargetFramework | Type hints | Has Program/Main | Has Controllers | Is Test Project |\n" >> "$OUT"
printf "|---|---:|---|---:|---:|---:|\n" >> "$OUT"

find . -name '*.csproj' -print0 | while IFS= read -r -d '' proj; do
  tf=$(xmllint --xpath "string(//TargetFramework|//TargetFrameworks)" "$proj" 2>/dev/null || echo "unknown")
  sdk=$(xmllint --xpath "string(/Project/@Sdk)" "$proj" 2>/dev/null || echo "")
  # type hints: look for OutputType or <Project Sdk="Microsoft.NET.Sdk.Web">
  outtype=$(xmllint --xpath "string(//OutputType)" "$proj" 2>/dev/null || echo "")
  webhint=""
  if grep -q "<Project Sdk=\"Microsoft.NET.Sdk.Web\"" "$proj" 2>/dev/null; then webhint="web-sdk"; fi
  if grep -q "<PackageReference.*Microsoft.AspNetCore" "$proj" 2>/dev/null; then webhint="${webhint} aspnet"; fi
  is_test="no"
  if grep -qE "Microsoft.NET.Test.Sdk|xunit|NUnit|MSTest" "$proj" 2>/dev/null; then is_test="yes"; fi
  has_main="no"
  if grep -qE "static\s+void\s+Main|WebApplication\.CreateBuilder|CreateHostBuilder" -- "$proj" -R 2>/dev/null; then has_main="maybe"; fi
  has_controllers="no"
  if grep -q "<Controller" -R -- **/*.cs 2>/dev/null || grep -q "Controller" -R -- **/*Controller*.cs 2>/dev/null; then has_controllers="maybe"; fi

  printf "| %s | %s | %s %s | %s | %s | %s |\n" "$proj" "$tf" "$sdk" "$webhint" "$has_main" "$has_controllers" "$is_test" >> "$OUT"
done

echo "" >> "$OUT"
echo "## Projects with Program/Main or WebApplication usage (quick scan)" >> "$OUT"
grep -R --line-number -E "static\s+void\s+Main|WebApplication\.CreateBuilder|CreateHostBuilder" -- **/*.cs || true >> "$OUT"

echo "" >> "$OUT"
echo "## Test projects and test frameworks" >> "$OUT"
grep -R --line-number "<PackageReference.*(Microsoft.NET.Test.Sdk|xunit|NUnit|MSTest)" -- **/*.csproj || true >> "$OUT"

echo "" >> "$OUT"
echo "## Large directories (top 20)" >> "$OUT"
du -ah --max-depth=2 2>/dev/null | sort -h | tail -n 20 >> "$OUT" || true

echo "" >> "$OUT"
echo "## Notes and suggested next steps" >> "$OUT"
echo "- Review projects marked as 'web-sdk' or containing ASP.NET packages for actual runtime code." >> "$OUT"
echo "- Open projects with 'maybe' for Program/Main and inspect the files listed above." >> "$OUT"
echo "- Check test projects for meaningful tests; run `dotnet test` to see pass/fail." >> "$OUT"

echo "Report written to $OUT"


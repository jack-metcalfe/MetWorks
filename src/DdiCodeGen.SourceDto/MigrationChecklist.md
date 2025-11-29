🔧 Migration Checklist: Source DTO Assembly
1. Establish the canonical DTO project
Create src/DdiCodeGen.SourceDto/ with:

minimal .csproj: net8.0, implicitUsings=false, nullable=enable, langVersion=latest.

README.md: explains purpose, principles, and usage.

Models folder: contains all DTO classes.

2. Audit existing assemblies
Search for duplicate DTOs across all projects.

Identify assemblies currently carrying local DTO definitions.

Document mismatches (e.g., casing, nullability, property defaults).

3. Refactor DTO usage
Move DTOs into DdiCodeGen.SourceDto.Models.

Update namespaces consistently (DdiCodeGen.SourceDto.Models).

Remove redundant DTOs from other assemblies.

Add explicit references to DdiCodeGen.SourceDto.csproj where needed.

4. Validate builds
Run dotnet build on each refactored assembly.

Check for missing references and fix with dotnet add reference.

Confirm DTO usage compiles cleanly across the solution.

5. Enforce with CI
Add a CI step to ensure no DTOs exist outside SourceDto.

Smoke test template instantiation with and without references.

Fail fast if duplicate DTOs are detected.

6. Document for contributors
Update solution README with rationale: DTOs are centralized, references are opt‑in.

Provide examples for adding references manually.

Explain helper scripts (add-references.sh / .cmd) for convenience.
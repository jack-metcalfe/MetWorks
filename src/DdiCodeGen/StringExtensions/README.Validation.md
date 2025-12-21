Here’s the validation suite outline for your new DdiCodeGen.Validation assembly. This gives you a clear scope of what rules to implement, so DTOs are guaranteed clean before any generator runs:

🔹 Validation Suite Outline
1. Identifier Rules
IsValidIdentifier: must be a valid C# identifier (letter/underscore start, letters/digits/underscores only, not a keyword).

IsPascalCase: enforce PascalCase for named instances.

EnsureValidIdentifier: throw diagnostic if invalid.

2. Qualified Names
IsQualifiedName: must be Namespace.ClassName format.

EnsureQualifiedName: throw if missing namespace or class segment.

IsInterfaceName: enforce I prefix for interfaces.

3. Lifetime Validation
IsValidLifetime: must be one of Singleton, Scoped, Transient.

EnsureLifetime: throw diagnostic if invalid or missing.

4. Provenance Metadata
HasProvenance: ensure provenance info is present.

EnsureProvenance: throw if missing.

EmbedDiagnosticCode: attach diagnostic codes to invalid DTOs.

5. Assignment Mode / Exclusivity
IsValidAssignmentMode: must be Initializer or Array.

EnsureMutualExclusivity: enforce “only one or neither” rule for assignment vs element arrays.

6. Non‑Nullability
IsNonNullable: ensure required fields are not null/empty.

EnsureNonNullable: throw diagnostic if missing.

7. Uniqueness
IsUniqueSafeKey: enforce uniqueness across all DTOs.

EnsureUniqueSafeKey: throw diagnostic if duplicate.
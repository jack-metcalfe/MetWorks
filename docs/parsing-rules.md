Validation Rules for isExposed
Rule 1: Presence

isExposed must always be present in raw DTOs (DDR‑011).

Transformer copies it directly into canonical DTOs.

Rule 2: Consistency

If isExposed = true → the class must have a valid QualifiedInterfaceName.

If isExposed = false → the class must not have a QualifiedInterfaceName.

Rule 3: Diagnostics

If isExposed = true but no interface is defined → emit Diagnostic(Error, "EXPOSED_NO_INTERFACE").

If isExposed = false but an interface is defined → emit Diagnostic(Warning, "INTERNAL_WITH_INTERFACE").

🔍 Validation Rules for Assignment/Element Exclusivity
Rule 1: Exclusivity

A named instance may contain at most one of:

assignments

elements

neither (both null or empty)

Rule 2: Diagnostics

If both assignments and elements are non‑empty → emit Diagnostic(Error, "INSTANCE_MIXED_ASSIGNMENTS_ELEMENTS").

If both are null or empty → valid, but transformer should normalize them to empty lists for canonical DTOs.

🔍 Transformer Checklist
Copy raw → canonical for all fields, including isExposed.

Validate exposure:

Check interface presence vs isExposed.

Emit diagnostics if inconsistent.

Validate exclusivity:

Ensure only one of assignments or elements is populated.

Emit diagnostics if both are present.

Normalize collections:

Canonical DTOs should always have non‑null lists ([]) even if raw had null.

Attach provenance + diagnostics to every canonical DTO.

This gives you a deterministic transformer contract: raw DTOs are permissive, canonical DTOs are strict, and diagnostics capture violations.
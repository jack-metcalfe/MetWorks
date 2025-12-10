# Simplification Design Decisions
*Date: 2025‑12‑03*

These records capture intentional choices to keep the pipeline lean, deterministic, and testable. They are not speculative; each decision explicitly avoids support for future enhancements unless revisited later.

---

## DDR‑001: Global Initializer Name
- **Decision**: Use a single global initializer name for all classes.
- **Rationale**: Simplifies authoring and generation; avoids per‑class overrides or conditional logic.
- **Implication**: All classes share the same initializer method name. No support for per‑class customization.

---

## DDR‑002: Parameter Type Disambiguation
- **Decision**: Parameters must explicitly declare either `QualifiedClassName` or `QualifiedInterfaceName`.
- **Rationale**: Eliminates guessing in the transformer; makes author intent explicit.
- **Implication**: Exactly one field must be non‑null. No support for ambiguous or inferred parameter types.

---

## DDR‑003: Named Instance Return Type
- **Decision**: A named instance’s `ReturnType` is always its interface if present, otherwise its `QualifiedClassName`.
- **Rationale**: Keeps generation deterministic and avoids conditional logic in templates.
- **Implication**: No support for alternative resolution strategies.

---

## DDR‑004: ExposeAsQualifiedInterfaceName Dropped
- **Decision**: Removed `ExposeAsQualifiedInterfaceName` from `NamedInstanceDto`.
- **Rationale**: The class already carries its interface; duplication adds complexity.
- **Implication**: Generators derive exposure from the class definition only.

---

## DDR‑005: Qualified Class Name Format
- **Decision**: `QualifiedClassName` is defined as `namespace1.namespace2.ClassName`.
- **Rationale**: Enforces a deterministic, compiler‑friendly naming convention.
- **Implication**: No support for assembly suffixes or alternate formats.

---

## DDR‑006: Templates as Dumb Token Holders
- **Decision**: Templates contain only simple tokens (e.g., `{{ReturnType}}`, `{{QualifiedClassName}}`).
- **Rationale**: Keeps generation deterministic and testable; logic lives in transformer/generator.
- **Implication**: No support for conditional expressions or advanced templating features.

---

## DDR‑007: Explicit Presence of All Elements
- **Decision**: All fields must be present in YAML fixtures, even if empty (`""`), `null`, or `[]`.
- **Rationale**: Reinforces schema determinism, simplifies parser/transformer logic, and avoids ambiguity in deserialization.
- **Implication**: Fixtures are longer but more complete. No support for omitting fields entirely.

---

## DDR‑008: Empty String vs Null
- **Decision**: Use `null` for absent values (e.g., no interface implemented, no assignment target).  
  Use `""` only for explicitly empty but present values (e.g., an intentionally blank string literal).  
- **Rationale**: Distinguishes between “not provided” (`null`) and “provided but empty” (`""`), avoiding conflation.  
- **Implication**: Contributors must choose consistently: `null` = absent, `""` = explicitly empty. No silent omission.

---

## DDR‑009: Async Initializer
- **Decision**: Require all generated classes to have a parameterless constructor and an async initializer named `InitializeAsync`.
- **Rationale**: Enforces a consistent async initialization pattern and simplifies generator logic.
- **Implication**: No support for synchronous initializers or parameterized constructors.

---

## DDR‑010: Named Instance Assignment Exclusivity
- **Decision**: A named instance may contain at most one of: `assignments`, `elements`, or neither.  
- **Rationale**: Prevents ambiguous configurations and simplifies transformer validation.  
- **Implication**: No support for mixing assignments and elements in the same instance. Violations must produce diagnostics.

## DDR‑011: Explicit Exposure Flag
- **Decision**: Add a boolean property `isExposed` to `NamedInstanceDto` to indicate whether the instance is exposed via an interface.
- **Rationale**: Makes exposure explicit, simplifies generator logic, and reinforces schema clarity.
- **Implication**: Transformer must compute and set `isExposed` consistently:
  - `isExposed = true` if `QualifiedInterfaceName` is present.
  - `isExposed = false` if only `QualifiedClassName` is present.
  - Violations (e.g., `isExposed = true` but no interface) must produce diagnostics.

## DDR‑012: Canonical Naming Alignment
- **Decision**: Canonical DTOs rename certain fields for clarity and consistency:
  - `RawNamedInstanceAssignmentDto.ParameterName` → `NamedInstanceAssignmentDto.AssignmentParameterName`
  - `RawNamedInstanceAssignmentDto.AssignedValue` → `NamedInstanceAssignmentDto.Value`
  - `RawNamedInstanceAssignmentDto.AssignedNamedInstance` → `NamedInstanceAssignmentDto.NamedInstanceName`
- **Rationale**: Aligns field names with their semantic role in the canonical model, avoids ambiguity with other DTOs, and makes generated code more readable.
- **Implication**: Raw DTOs preserve YAML token names exactly; Canonical DTOs normalize names for clarity. Contributors must expect these renames when writing tests or migration guides.

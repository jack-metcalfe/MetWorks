Got it — what you’ve got are really two halves of the same playbook. One was spelling out the canonical DTOs and their evolution, the other was a pipeline walkthrough with tasks, tests, and rationale. They overlap because both are trying to make the pipeline deterministic and testable. Let me merge them into a single, coherent document that you can use as your “north star” when stepping through the code.

Deterministic Pipeline Guide
1. Pipeline order and responsibilities
Parse YAML → Raw DTOs (DdiCodeGen.SourceDto.Parser) Convert YAML into raw DTO objects, attach provenance and source path metadata.

Transform Raw → Canonical DTOs (DdiCodeGen.SourceDto) Normalize raw DTOs into canonical DTOs, enforce invariants, compute derived fields like ReturnType.

Validate Canonical DTOs (Validation.Exchange, Core.Diagnostics) Apply validation rules, produce diagnostics, propagate provenance.

Generate Code (DdiCodeGen.Generator, DdiCodeGen.Templates.StaticDataStore) Render templates using canonical DTOs, substitute tokens deterministically, escape type keys.

Support Libraries (Core.Models, Core.Diagnostics) Shared DTOs, helpers, escaping utilities, provenance helpers.

2. Canonical DTOs (finalized shape)
csharp
namespace DdiCodeGen.SourceDto.Canonical;

using System;
using System.Collections.Generic;

public sealed record CanonicalModelDto(
    CodeGenDto CodeGen,
    IReadOnlyList<NamespaceDto> Namespaces,
    IReadOnlyList<NamedInstanceDto> NamedInstances,
    string SourcePath,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record CodeGenDto(
    string RegistryClassName,
    string GeneratedCodePath,
    string NamespaceName,
    string InitializerName,    // global initializer name, default "Create"
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record NamespaceDto(
    string NamespaceName,
    IReadOnlyList<InterfaceDto> Interfaces,
    IReadOnlyList<ClassDto> Classes,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record InterfaceDto(
    string InterfaceName,
    string QualifiedInterfaceName,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record ClassDto(
    string ClassName,
    string QualifiedClassName,
    string? QualifiedInterfaceName,
    IReadOnlyList<ParameterDto> InitializerParameters,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record ParameterDto(
    string ParameterName,
    string? QualifiedClassName,
    string? QualifiedInterfaceName,
    bool IsValid,
    IReadOnlyList<Diagnostic> Diagnostics,
    ProvenanceStack ProvenanceStack
);

public sealed record NamedInstanceDto(
    string NamedInstanceName,
    string QualifiedClassName,
    IReadOnlyList<NamedInstanceAssignmentDto> Assignments,
    IReadOnlyList<NamedInstanceElementDto> Elements,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record NamedInstanceAssignmentDto(
    string AssignmentParameterName,
    string? Value,
    string? NamedInstanceName,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record NamedInstanceElementDto(
    string? Value,
    string? NamedInstanceName,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);

public sealed record NamedInstanceAccessorDto(
    string AccessorClassName,
    string AccessorNamespace,
    string TargetQualifiedClassName,
    string? ExposeAsQualifiedInterfaceName,
    ProvenanceStack ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);
3. Stage‑by‑stage tasks and tests
Parse stage
Files: SourceDtoParser.cs, YAML mapping helpers, raw DTOs.

Checks: provenance attached, SourcePath set, malformed YAML produces diagnostics not null.

Tests:

Assert provenance contains parser stage.

Assert raw DTO fields match YAML scalars/lists/nulls.

Negative test for malformed YAML.

Transform stage
Files: RawToCanonicalTransformer.cs, canonical DTOs, validators.

Checks:

Normalize names and defaults.

Compute ReturnType = QualifiedInterfaceName ?? QualifiedClassName.

Enforce mutual exclusivity of parameter tokens.

Append transformer provenance.

Tests:

Fixture‑driven parse → transform → assert invariants.

Assert ReturnType set correctly.

Assert illegal combos rejected with diagnostics.

Validation
Files: Validation.Exchange, Core.Diagnostics.

Checks: stable diagnostic codes/messages, provenance append semantics.

Tests:

Unit tests for each validation rule.

Tests verifying provenance entries include stage and source path.

Generation
Files: AccessorGenerator.cs, TemplateRenderer, CodeEscaping.

Checks:

Templates use only tokens like {{ReturnType}}.

Substitution helper replaces tokens deterministically.

Escape type keys for string literals.

Output always includes namespace, registry class, constructor, accessors, resolve helper.

Tests:

Integration tests parser → transformer → generator.

Snapshot tests for golden fixtures.

Optional Roslyn compile tests.

Support libraries
Files: Core.Models, helpers.

Checks: canonical DTOs expose generator‑needed fields.

Tests: unit tests for helpers like EscapeTypeKey.

4. Immediate actions
Open SourceDtoParser.cs and confirm provenance and source path.

Open RawToCanonicalTransformer.cs and implement ReturnType computation and illegal‑combo policy.

Open AccessorGenerator.cs and ensure token substitution uses ReturnType.

Update Accessor.template to use only {{ReturnType}} and {{QualifiedClassName}}.

Add fixtures exposed-instance.yaml and internal-instance.yaml.

Run tests stage by stage.

5. Technology rationale
YAML chosen for readability and comments.

Two‑stage ingestion separates parsing fidelity from normalization.

Canonical DTOs hold derived values so templates stay dumb.

Templates are token holders only; logic lives in transformer/generator.

Testing mirrors pipeline order for clarity.
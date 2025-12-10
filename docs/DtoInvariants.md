Overview
This document captures the canonical invariants the normalizer enforces when converting Raw DTOs (YAML shape) into Canonical DTOs under DdiCodeGen.SourceDto.Canonical. Place this file next to the canonical DTOs (e.g., DtoInvariants.md) so contributors and normalizer implementers share a single source of truth.

Purpose: make normalization rules explicit, deterministic, and testable.

Scope: applies only to canonical DTOs. The normalizer is the single place that applies defaults and enforces invariants.

General Principles
Single source of defaults: defaults are applied only in the normalizer; canonical DTOs are immutable and assume invariants hold.

Fail early: normalization fails with clear, provenance-rich errors when an invariant is violated.

Determinism: the same raw input must always produce the same canonical DTO.

Provenance: every canonical DTO must carry a ProvenanceStack that is non-null and contains at least one ProvenanceEntry.

Normalization order: validate provenance first, apply defaults, validate structure, validate cross-entity invariants, resolve parameter bindings, detect cycles, finalize canonical DTOs.

DTO Specific Invariants
ProvenanceStack
Invariant: Entries must be non-empty.

Invariant: Version must be >= ProvenanceStack.MinVersion.

Default: If Version is missing in raw input, set to ProvenanceStack.MinVersion.

Error: ProvenanceStack must contain at least one entry; source: {LogicalPath}.

ProvenanceEntry and ProvenanceOrigin
Invariant: Origin.LogicalPath must be non-empty.

Invariant: Origin.SourcePath must be set; use "<in-memory>" sentinel when not from a file.

Default: LineZeroBased defaults to 0 when not available.

Error: ProvenanceOrigin LogicalPath is required; source: {SourcePath}:{LineZeroBased}.

ConfigurationDto
Invariant: CodeGen must be non-null.

Invariant: Namespaces and NamedInstances must be non-null lists (use empty list when absent).

Invariant: SourcePath must be non-empty.

Error: Configuration missing CodeGen section; source: {LogicalPath}.

CodeGenDto
Invariant: RegistryClassName, GeneratedCodePath, and NamespaceName must be non-empty.

Error: CodeGen.NamespaceName is required; source: {LogicalPath}.

NamespaceDto
Invariant: NamespaceName must be non-empty.

Default: Classes and Interfaces default to empty lists when absent.

Error: Namespace missing name; source: {LogicalPath}.

ClassDto
Invariant: ClassName and QualifiedClassName must be non-empty and consistent. The normalizer should construct QualifiedClassName from NamespaceName + ClassName if needed.

Invariant: Only one initializer allowed, represented as InitializerName and InitializerParameters.

Default: InitializerParameters defaults to empty list.

Error: Class {QualifiedClassName} must have at most one initializer; source: {LogicalPath}.

Initializer Parameters
Invariant: InitializerName must be non-empty when present.

Invariant: InitializerParameters must be a non-null list (use empty list when absent).

Error: Initializer name missing for class {QualifiedClassName}; source: {LogicalPath}.

ParameterDto
Invariant: ParameterName must be non-empty.

Invariant: Exactly one of QualifiedClassName or QualifiedInterfaceName must be non-null.

Error: Parameter {ParameterName} must reference either a class or an interface, not both or neither; source: {LogicalPath}.

InterfaceDto
Invariant: InterfaceName and QualifiedInterfaceName must be non-empty.

Error: Interface missing name in namespace {NamespaceName}; source: {LogicalPath}.

NamedInstanceDto
Invariant: NamedInstanceName and ClassName must be non-empty.

Default: ExposeAsInterfaceName may be null.

Default: Assignments and Elements default to empty lists.

Invariant: NamedInstanceName must be unique within the configuration.

Error: Duplicate named instance {NamedInstanceName} found; sources: {LogicalPaths}.

NamedInstanceAssignmentDto and NamedInstanceElementDto
Invariant: AssignmentParameterName must be non-empty for assignments.

Invariant: Exactly one of Value or NamedInstanceName must be non-null.

Error: Assignment for parameter {AssignmentParameterName} must provide either a literal Value or a NamedInstanceName; source: {LogicalPath}.

Normalizer Behavior and Error Messages
Validation order:

Normalize and validate provenance.

Apply top-level defaults (lists → empty, version → MinVersion).

Validate structural invariants (required fields present).

Validate cross-entity invariants (unique named instance names, class/interface consistency).

Resolve parameter bindings and detect ambiguity or cycles.

Finalize canonical DTOs and attach final provenance entry for normalization stage.

Error message format: always include what failed, where (logical path and line if available), and why (expected type or candidate list). Include provenance summary from ProvenanceStack.Latest.

Example templates:

InvariantViolation: {What} at {SourcePath}:{LineZeroBased} — {Detail}. Provenance: {ProvenanceSummary}.

AmbiguousResolution: Parameter {ParameterName} in {QualifiedClassName} has multiple candidate named instances: {Candidates}. Provenance: {ProvenanceSummary}.

MissingBinding: No candidate found for parameter {ParameterName} in {QualifiedClassName}. Expected type: {QualifiedTypeName}. Provenance: {ProvenanceSummary}.

ProvenanceSummary: include stage, tool, timestamp, and Origin.LogicalPath.

Tests and Examples
Unit tests to include

Provenance: missing Entries fails.

Class initializer: two initializers in YAML fails.

Parameter duality: both class and interface set fails; neither set fails.

Named instance uniqueness: duplicate names fail.

Assignment duality: assignment with both Value and NamedInstanceName fails; neither set fails.

Defaults: missing lists become empty lists; missing version becomes MinVersion.

Example unit test

csharp
[Fact]
public void ParameterMustReferenceExactlyOneType()
{
    var raw = new RawParameter { ParameterName = "p", QualifiedClassName = null, QualifiedInterfaceName = null };
    var ex = Assert.Throws<NormalizationException>(() => NormalizeParameter(raw));
    Assert.Contains("must reference either a class or an interface", ex.Message);
}
Worked example

Include a short YAML snippet in the repo docs showing raw YAML → raw DTO → canonical DTO after normalization, highlighting defaults and provenance entries. Keep one example for a simple class with one initializer and one named instance resolution.

Implementation Checklist
[ ] Ensure ProvenanceStack is created or extended with a normalization entry.

[ ] Apply Version default to ProvenanceStack.MinVersion when missing.

[ ] Convert absent lists to Array.Empty<T>() for canonical DTOs.

[ ] Enforce non-empty string invariants for names and qualified names.

[ ] Enforce duality invariants (exactly one of two nullable fields).

[ ] Enforce uniqueness constraints (named instances).

[ ] Resolve parameter bindings and fail on ambiguity or missing candidates.

[ ] Detect and fail on circular dependencies among named instances/initializers.

[ ] Produce standardized error messages including provenance.

[ ] Add unit tests for each invariant and a small set of end-to-end generation tests.

[ ] Update repository docs with a worked YAML → raw DTO → canonical DTO example.

Tip Keep the normalizer as the single place where schema decisions live. When an invariant changes, update this document and the normalizer together so contributors and code remain in sync.
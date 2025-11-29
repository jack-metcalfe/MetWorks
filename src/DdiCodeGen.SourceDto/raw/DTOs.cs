namespace DdiCodeGen.SourceDto.Raw;

using System;
using System.Collections.Generic;

// Provenance (raw, permissive)
public sealed record RawProvenanceOrigin(
    string? SourcePath,
    int? LineZeroBased,
    int? ColumnZeroBased,
    string? LogicalPath
);

public sealed record RawProvenanceEntry(
    RawProvenanceOrigin? Origin,
    string? Stage,
    string? Tool,
    DateTimeOffset? When
);

public sealed record RawProvenanceStack(
    int? Version,
    IReadOnlyList<RawProvenanceEntry>? Entries
);

// Top level configuration (maps to the YAML root)
public sealed record RawConfigurationDto(
    RawCodeGenDto? CodeGen,
    IReadOnlyList<RawAssemblyDto>? Assemblies,
    IReadOnlyList<RawNamedInstanceDto>? NamedInstances,
    IReadOnlyList<RawNamespaceDto>? Namespaces,
    string? SourcePath,
    RawProvenanceStack? Provenance
);

// codeGen section
public sealed record RawCodeGenDto(
    string? RegistryClass,
    string? GeneratedCodePath,
    string? ResourceProvider,
    string? Namespace,
    bool? FailFast,
    IReadOnlyList<RawCodeGenEnumsDto>? Enums,
    RawProvenanceStack? Provenance
);

public sealed record RawCodeGenEnumsDto(
    string? EnumName,
    string? Scope,
    RawProvenanceStack? Provenance
);

// assemblies
public sealed record RawAssemblyDto(
    string? Assembly,
    string? FullName,
    string? Path,
    bool? Primitive,
    RawProvenanceStack? Provenance
);

// namespaces
public sealed record RawNamespaceDto(
    string? Namespace,
    IReadOnlyList<RawTypeDto>? Types,
    IReadOnlyList<RawInterfaceDto>? Interfaces,
    RawProvenanceStack? Provenance
);

// types within namespaces
public sealed record RawTypeDto(
    string? Type,
    string? FullName,
    string? Assembly,
    string? TypeKind,
    int? GenericArity,
    IReadOnlyList<string>? GenericParameterNames,
    IReadOnlyList<RawConstructorSpecDto>? Constructors,
    IReadOnlyList<RawInitializerDto>? Initializers,
    IReadOnlyList<string>? Attributes,
    IReadOnlyList<string>? ImplementedInterfaces,
    bool? Assignable,
    RawProvenanceStack? Provenance
);

// constructors (per earlier drafts; present in raw to reflect any input or future YAML)
public sealed record RawConstructorSpecDto(
    IReadOnlyList<RawParameterDto>? Parameters,
    RawProvenanceStack? Provenance,
    string? VisibilityHint
);

// initializers and parameters
public sealed record RawInitializerDto(
    string? Initializer,
    bool? Eager,
    int? Order,
    IReadOnlyList<RawParameterDto>? Parameters,
    RawProvenanceStack? Provenance
);

public sealed record RawParameterDto(
    string? Parameter,
    string? Type,
    string? Interface,
    RawProvenanceStack? Provenance
);

// interfaces declared in namespaces
public sealed record RawInterfaceDto(
    string? Interface,
    string? Assembly,
    RawProvenanceStack? Provenance
);

// named instances
public sealed record RawNamedInstanceDto(
    string? NamedInstance,
    string? Type,
    string? AssignmentMode,
    string? Initializer,
    bool? EagerLoad,
    string? ExposeAsInterface,   // renamed in raw model
    bool? FailFast,
    RawNamedInstanceAssignmentDto[]? Assignments,
    RawNamedInstanceElementDto[]? Elements,
    RawProvenanceStack? Provenance
);
public sealed record RawNamedInstanceAssignmentDto(
    string? Assignment,
    string? Value,
    string? NamedInstance,
    RawProvenanceStack? Provenance
);

public sealed record RawNamedInstanceElementDto(
    string? Value,
    string? NamedInstance,
    RawProvenanceStack? Provenance
);

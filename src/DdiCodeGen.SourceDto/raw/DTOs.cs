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
    IReadOnlyList<RawProvenanceEntry> Entries,
    int Version = 1
);

// Top level configuration (maps to the YAML root)
public sealed record RawConfigurationDto(
    RawCodeGenDto? CodeGen,
    IReadOnlyList<RawAssemblyDto> Assemblies,
    IReadOnlyList<RawNamedInstanceDto> NamedInstances,
    IReadOnlyList<RawNamespaceDto> Namespaces,
    string? SourcePath,
    RawProvenanceStack? ProvenanceStack
);

// codeGen section
public sealed record RawCodeGenDto(
    string? RegistryClass,
    string? GeneratedCodePath,
    string? ResourceProvider,
    string? Namespace,
    bool? FailFast,
    IReadOnlyList<RawCodeGenEnumsDto> Enums,
    RawProvenanceStack? ProvenanceStack
);

public sealed record RawCodeGenEnumsDto(
    string? EnumName,
    string? Scope,
    RawProvenanceStack? ProvenanceStack
);

// assemblies
public sealed record RawAssemblyDto(
    string? Assembly,
    string? FullName,
    string? Path,
    bool? Primitive,
    RawProvenanceStack? ProvenanceStack
);

// namespaces
public sealed record RawNamespaceDto(
    string? Namespace,
    IReadOnlyList<RawTypeDto> Types,
    IReadOnlyList<RawInterfaceDto> Interfaces,
    RawProvenanceStack? ProvenanceStack
);

// types within namespaces
public sealed record RawTypeDto(
    string? Type,
    string? FullName,
    string? Assembly,
    string? TypeKind,
    IReadOnlyList<RawInitializerDto> Initializers,
    IReadOnlyList<string> Attributes,
    IReadOnlyList<string> ImplementedInterfaces,
    bool? Assignable,
    RawProvenanceStack? ProvenanceStack
);


// initializers and parameters
public sealed record RawInitializerDto(
    string? Initializer,
    bool? Eager,
    int? Order,
    IReadOnlyList<RawParameterDto> Parameters,
    RawProvenanceStack? ProvenanceStack
);

public sealed record RawParameterDto(
    string? Parameter,
    string? Type,
    string? Interface,
    RawProvenanceStack? ProvenanceStack
);

// interfaces declared in namespaces
public sealed record RawInterfaceDto(
    string? Interface,
    string? Assembly,
    RawProvenanceStack? ProvenanceStack
);

// named instances
public sealed record RawNamedInstanceDto(
    string? NamedInstance,
    string? Type,
    string? AssignmentMode,
    string? Initializer,
    bool? EagerLoad,
    string? ExposeAsInterfaceName,   // renamed in raw model
    bool? FailFast,
    IReadOnlyList<RawNamedInstanceAssignmentDto> Assignments,
    IReadOnlyList<RawNamedInstanceElementDto> Elements,
    RawProvenanceStack? ProvenanceStack
);
public sealed record RawNamedInstanceAssignmentDto(
    string? Assignment,
    string? Value,
    string? NamedInstance,
    RawProvenanceStack? ProvenanceStack
);

public sealed record RawNamedInstanceElementDto(
    string? Value,
    string? NamedInstance,
    RawProvenanceStack? ProvenanceStack
);

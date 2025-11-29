namespace DdiCodeGen.SourceDto.Canonical;

using System;
using System.Collections.Generic;

public sealed record ProvenanceOrigin(
    string SourcePath,        // "<in-memory>" when not from a file
    int LineZeroBased,       // 0 when not available
    int? ColumnZeroBased,    // optional
    string LogicalPath       // required, non-empty
);

public sealed record ProvenanceEntry(
    ProvenanceOrigin Origin,
    string Stage,            // "parser", "normalizer", "generator", etc.
    string Tool,             // e.g., "yaml-parser-v1"
    DateTimeOffset When      // UTC timestamp
);

public sealed record ProvenanceStack(
    int Version,
    IReadOnlyList<ProvenanceEntry> Entries
)
{
    public ProvenanceEntry Latest => Entries[^1];
}

// Top-level configuration
public sealed record ConfigurationDto(
    CodeGenDto CodeGen,
    IReadOnlyList<AssemblyDto> Assemblies,
    IReadOnlyList<NamedInstanceDto> NamedInstances,
    IReadOnlyList<NamespaceDto> Namespaces,
    string SourcePath,
    ProvenanceStack ProvenanceStack
);

// codeGen section
public sealed record CodeGenDto(
    string RegistryClass,
    string GeneratedCodePath,
    string ResourceProvider,
    string Namespace,
    bool FailFast,
    IReadOnlyList<CodeGenEnumsDto> Enums,
    NamedInstanceAccessorDto? NamedInstanceAccessor,
    ProvenanceStack ProvenanceStack
);

public sealed record CodeGenEnumsDto(
    string EnumName,
    string Scope,
    ProvenanceStack ProvenanceStack
);

public sealed record NamedInstanceAccessorDto(
    string Class,
    ProvenanceStack ProvenanceStack
);

// assemblies
public sealed record AssemblyDto(
    string Assembly,
    string FullName,
    string Path,
    bool Primitive,
    ProvenanceStack ProvenanceStack
);

// namespaces
public sealed record NamespaceDto(
    string Namespace,
    IReadOnlyList<TypeDto> Types,
    IReadOnlyList<InterfaceDto> Interfaces,
    ProvenanceStack ProvenanceStack
);

// types within namespaces
public sealed record TypeDto(
    string Type,
    string FullName,
    string Assembly,
    string TypeKind,
    int GenericArity,
    IReadOnlyList<string> GenericParameterNames,
    IReadOnlyList<InitializerDto> Initializers,
    IReadOnlyList<string> Attributes,
    IReadOnlyList<string> ImplementedInterfaces,
    bool Assignable,
    ProvenanceStack ProvenanceStack
);

// initializers and parameters
public sealed record InitializerDto(
    string Initializer,
    bool Eager,
    int Order,
    IReadOnlyList<ParameterDto> Parameters,
    ProvenanceStack ProvenanceStack
);

public sealed record ParameterDto(
    string Parameter,
    string? Type,
    string? Interface,
    ProvenanceStack ProvenanceStack
);

// interfaces declared in namespaces
public sealed record InterfaceDto(
    string Interface,
    string Assembly,
    ProvenanceStack ProvenanceStack
);

// named instances
public sealed record NamedInstanceDto(
    string NamedInstance,
    string Type,
    string AssignmentMode,
    string? Initializer,
    bool EagerLoad,
    string? ExposeAsInterface,   // renamed to be explicit
    bool FailFast,
    NamedInstanceAssignmentDto[] Assignments,
    NamedInstanceElementDto[] Elements,
    ProvenanceStack ProvenanceStack
);

public sealed record NamedInstanceAssignmentDto(
    string Assignment,
    string? Value,
    string? NamedInstance,
    ProvenanceStack ProvenanceStack
);

public sealed record NamedInstanceElementDto(
    string? Value,
    string? NamedInstance,
    ProvenanceStack ProvenanceStack
);
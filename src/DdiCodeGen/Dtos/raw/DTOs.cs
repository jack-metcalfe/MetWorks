namespace DdiCodeGen.Dtos.Raw;

// Provenance primitives (raw, permissive)
public sealed record RawProvenanceOrigin(
    string SourcePath,
    int LineZeroBased,
    int ColumnZeroBased,
    string? LogicalPath
);

public sealed record RawProvenanceEntry(
    RawProvenanceOrigin Origin,
    string Stage,
    string Tool,
    DateTimeOffset When
);

public sealed record RawProvenanceStack(
    int Version,
    IReadOnlyList<RawProvenanceEntry> Entries
);

// Top level model (parsed YAML root)
public sealed record RawModelDto(
    RawCodeGenDto? CodeGen,
    IReadOnlyList<RawNamespaceDto> Namespaces,
    IReadOnlyList<RawNamedInstanceDto> NamedInstances,
    string SourcePath,
    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // structural diagnostics at root
);

public sealed record PackageReferenceDto(string Id, string? Version);
public sealed record RawCodeGenDto(
    string? RegistryClassName,
    string? GeneratedCodePath,
    string? NamespaceName,
    string? InitializerName,
    IReadOnlyList<PackageReferenceDto>? PackageReferences,
    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics
);


public sealed record RawInterfaceDto(
    string? InterfaceName,
    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // diagnostics for interface mapping
);

public sealed record RawNamespaceDto(
    string? NamespaceName,
    IReadOnlyList<RawInterfaceDto> Interfaces,
    IReadOnlyList<RawClassDto> Classes,
    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // diagnostics for namespace mapping
);

// Class declaration inside a namespace: short ClassName only; transformer derives QualifiedClassName
public sealed record RawClassDto(
    string? ClassName,
    string? QualifiedInterfaceName,
    IReadOnlyList<RawParameterDto>? InitializerParameters,
    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // diagnostics for class mapping
);

// Parameter now requires explicit disambiguation: either QualifiedClassName or QualifiedInterfaceName
// Parameter now requires explicit disambiguation: either QualifiedClassName or QualifiedInterfaceName
public sealed record RawParameterDto(
    string? ParameterName,

    // Original tokens (may include modifiers)
    string? QualifiedClassName,
    string? QualifiedInterfaceName,

    // NEW: parsed base names and flags
    string? QualifiedClassBaseName,
    bool QualifiedClassIsArray,
    bool QualifiedClassIsContainerNullable,
    bool QualifiedClassElementIsNullable,

    string? QualifiedInterfaceBaseName,
    bool QualifiedInterfaceIsArray,
    bool QualifiedInterfaceIsContainerNullable,
    bool QualifiedInterfaceElementIsNullable,

    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // diagnostics for parameter mapping
);


// Named instance references a fully qualified class token; order in the list is significant
public sealed record RawNamedInstanceDto(
    string? NamedInstanceName,

    // Original token (may include modifiers)
    string? QualifiedClassName,

    // NEW: parsed base name and flags
    string? QualifiedClassBaseName,
    bool QualifiedClassIsArray,
    bool QualifiedClassIsContainerNullable,
    bool QualifiedClassElementIsNullable,

    // Optional interface exposure with modifiers
    string? QualifiedInterfaceName,
    string? QualifiedInterfaceBaseName,
    bool QualifiedInterfaceIsArray,
    bool QualifiedInterfaceIsContainerNullable,
    bool QualifiedInterfaceElementIsNullable,

    IReadOnlyList<RawNamedInstanceAssignmentDto> Assignments,
    IReadOnlyList<RawNamedInstanceElementDto> Elements,

    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // diagnostics for named instance mapping
);

// Assignment nested under a named instance binds a parameter on that named instance's class
public sealed record RawNamedInstanceAssignmentDto(
    string? AssignmentParameterName,
    string? AssignmentValue,
    string? AssignmentNamedInstanceName,
    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // diagnostics for assignment mapping
);

// Optional collection element shape (use only if you need explicit element entries)
public sealed record RawNamedInstanceElementDto(
    string? AssignmentValue,
    string? AssignmentNamedInstanceName,
    RawProvenanceStack? ProvenanceStack,
    IReadOnlyList<Diagnostic> Diagnostics          // diagnostics for element mapping
);

// Centralized schema: allowed keys per DTO type
internal static class RawYamlSchema
{
    public static readonly IReadOnlyDictionary<Type, string[]> AllowedKeys =
        new Dictionary<Type, string[]>
        {
            { typeof(RawCodeGenDto), new[] {
                    "registryClassName",
                    "generatedCodePath",
                    "namespaceName",
                    "initializerName",
                    "packageReferences"
                }
            },
            { typeof(RawNamespaceDto), new[] {
                    "namespaceName",
                    "interfaces",
                    "classes"
                }
            },
            { typeof(RawClassDto), new[] {
                    "className",
                    "qualifiedInterfaceName",
                    "initializerParameters"
                 }
            },
            { typeof(RawParameterDto), new[] {
                    "parameterName",
                    "qualifiedClassName",
                    "qualifiedInterfaceName"
                 }
            },
            { typeof(RawNamedInstanceDto), new[] {
                    "namedInstanceName",
                    "qualifiedClassName",
                    "assignments",
                    "elements"
                }
            },
            { typeof(RawNamedInstanceAssignmentDto), new[] {
                    "parameterName",
                    "assignedValue",
                    "assignedNamedInstance"
                }
            },
            { typeof(RawNamedInstanceElementDto), new[] { 
                    "assignedValue",
                    "assignedNamedInstance" 
                }
            },
            { typeof(RawInterfaceDto), new[] { 
                    "interfaceName"
                }
            }
        };
}

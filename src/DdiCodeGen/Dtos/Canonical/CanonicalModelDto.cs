namespace DdiCodeGen.Dtos.Canonical;

/// <summary>
/// Canonical root DTO representing the parsed and normalized YAML model.
/// Identifiers are guaranteed C#-safe at this boundary.
/// </summary>
public sealed record CanonicalModelDto : IHaveProvenance
{
    public CodeGenDto CodeGen { get; }
    public IReadOnlyList<NamespaceDto> Namespaces { get; }
    public IReadOnlyList<NamedInstanceDto> NamedInstances { get; }
    public string SourcePath { get; }
    public ProvenanceStack ProvenanceStack { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public CanonicalModelDto(
        CodeGenDto codeGen,
        IReadOnlyList<NamespaceDto>? namespaces,
        IReadOnlyList<NamedInstanceDto>? namedInstances,
        string sourcePath,
        ProvenanceStack provenanceStack,
        IReadOnlyList<Diagnostic>? diagnostics)
    {
        CodeGen = codeGen ?? throw new ArgumentNullException(nameof(codeGen));

        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("SourcePath is required and cannot be empty.", nameof(sourcePath));
        SourcePath = sourcePath;

        if (provenanceStack is null || provenanceStack.Entries.Count == 0)
            throw new ArgumentException("ProvenanceStack must be provided and non-empty.", nameof(provenanceStack));
        ProvenanceStack = provenanceStack;

        Namespaces = (namespaces ?? Array.Empty<NamespaceDto>()).ToList().AsReadOnly();
        NamedInstances = (namedInstances ?? Array.Empty<NamedInstanceDto>()).ToList().AsReadOnly();
        Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();

        // Uniqueness: (namespace, instance name) must be unique among NamedInstances.
        // Use QualifiedClassName -> SafeExtractNamespace to determine instance scope.
        var dupKeys = NamedInstances
            .GroupBy(i => (Namespace: i.QualifiedClassName.SafeExtractNamespace(), Name: i.NamedInstanceName))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Namespace}.{g.Key.Name}")
            .ToList();

        if (dupKeys.Count > 0)
            throw new ArgumentException($"Duplicate named instances within the same namespace: {string.Join(", ", dupKeys)}", nameof(namedInstances));
    }
}

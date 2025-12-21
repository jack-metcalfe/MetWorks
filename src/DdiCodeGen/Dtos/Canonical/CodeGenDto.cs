namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing global code generation settings.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed record CodeGenDto : IHaveProvenance
    {
        public string RegistryClassName { get; }
        public string GeneratedCodePath { get; }
        public string NamespaceName { get; }
        public string InitializerName { get; }
        public IReadOnlyList<PackageReferenceDto> PackageReferences { get; } = Array.Empty<PackageReferenceDto>();
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public CodeGenDto(
            string registryClassName,
            string generatedCodePath,
            string namespaceName,
            string initializerName,
            IReadOnlyList<PackageReferenceDto> packageReferences,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            registryClassName.EnsureValidIdentifier(nameof(registryClassName));
            if (!registryClassName.IsPascalCase())
                throw new ArgumentException("Registry class name must follow PascalCase convention.", nameof(registryClassName));
            RegistryClassName = registryClassName;

            if (string.IsNullOrWhiteSpace(generatedCodePath))
                throw new ArgumentException("Generated code path is required and cannot be empty.", nameof(generatedCodePath));
            GeneratedCodePath = generatedCodePath;

            if (!namespaceName.IsValidNamespace())
                throw new ArgumentException("Namespace must be a valid dot-separated namespace with valid identifier segments.", nameof(namespaceName));
            NamespaceName = namespaceName;

            initializerName.EnsureValidIdentifier(nameof(initializerName));
            if (!initializerName.IsPascalCase())
                throw new ArgumentException("Initializer name must follow PascalCase convention.", nameof(initializerName));
            InitializerName = initializerName;

            PackageReferences = packageReferences;

            ProvenanceStack = provenanceStack;

            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }
    }
}

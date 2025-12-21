namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing a constructor or initializer parameter.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed record ParameterDto : IHaveProvenance
    {
        public string ParameterName { get; }
        public string? QualifiedClassName { get; }
        public string? QualifiedInterfaceName { get; }

        // Modifiers
        public bool IsArray { get; }
        public bool IsNullable { get; }              // container nullability
        public bool IsValid { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public ProvenanceStack ProvenanceStack { get; }

        public ParameterDto(
            string parameterName,
            string? qualifiedClassName,
            string? qualifiedInterfaceName,
            bool isArray,
            bool isNullable,
            bool isValid,
            IReadOnlyList<Diagnostic>? diagnostics,
            ProvenanceStack provenanceStack)
        {
            parameterName.EnsureValidIdentifier(nameof(parameterName));
            ParameterName = parameterName;

            if (!string.IsNullOrWhiteSpace(qualifiedClassName) && !string.IsNullOrWhiteSpace(qualifiedInterfaceName))
                throw new ArgumentException("Parameter cannot specify both QualifiedClassName and QualifiedInterfaceName.");

            if (!string.IsNullOrWhiteSpace(qualifiedClassName))
                qualifiedClassName.EnsureQualifiedName(nameof(qualifiedClassName));
            QualifiedClassName = qualifiedClassName;

            if (!string.IsNullOrWhiteSpace(qualifiedInterfaceName))
            {
                qualifiedInterfaceName.EnsureQualifiedName(nameof(qualifiedInterfaceName));
                var shortName = qualifiedInterfaceName.ExtractShortName();
                if (!shortName.IsInterfaceName())
                    throw new ArgumentException("QualifiedInterfaceName must resolve to a valid interface name.", nameof(qualifiedInterfaceName));
            }
            QualifiedInterfaceName = qualifiedInterfaceName;

            IsArray = isArray;
            IsNullable = isNullable;

            IsValid = isValid;
            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();

            if (provenanceStack is null || ProvenanceStackEntriesEmpty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));
            ProvenanceStack = provenanceStack;
        }

        private static bool ProvenanceStackEntriesEmpty(ProvenanceStack stack) => stack.Entries == null || stack.Entries.Count == 0;
    }
}

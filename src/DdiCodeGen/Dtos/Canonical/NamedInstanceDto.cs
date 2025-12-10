namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing a named instance declaration from YAML input.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed record NamedInstanceDto
    {
        public string NamedInstanceName { get; }
        public string QualifiedClassName { get; }
        public string? QualifiedInterfaceName { get; }   // NEW: optional interface exposure

        // Class modifiers
        public bool IsArray { get; }                     // container is array
        public bool IsNullable { get; }                  // container nullability
        public bool ElementIsNullable { get; }           // element nullability

        // Interface modifiers (parallel to class)
        public bool InterfaceIsArray { get; }            // NEW
        public bool InterfaceIsNullable { get; }         // NEW
        public bool InterfaceElementIsNullable { get; }  // NEW

        public IReadOnlyList<NamedInstanceAssignmentDto> Assignments { get; }
        public IReadOnlyList<NamedInstanceElementDto> Elements { get; }
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public NamedInstanceDto(
            string namedInstanceName,
            string qualifiedClassName,
            string? qualifiedInterfaceName,              // NEW
            bool isArray,
            bool isNullable,
            bool elementIsNullable,
            bool interfaceIsArray,                       // NEW
            bool interfaceIsNullable,                    // NEW
            bool interfaceElementIsNullable,             // NEW
            IReadOnlyList<NamedInstanceAssignmentDto>? assignments,
            IReadOnlyList<NamedInstanceElementDto>? elements,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            // Validate name
            namedInstanceName.EnsureValidIdentifier(nameof(namedInstanceName));
            if (!namedInstanceName.IsPascalCase())
                throw new ArgumentException("Named instance name must follow PascalCase convention.", nameof(namedInstanceName));
            NamedInstanceName = namedInstanceName;

            // Qualified class name must be provided and valid
            if (string.IsNullOrWhiteSpace(qualifiedClassName))
                throw new ArgumentException("QualifiedClassName must be provided.", nameof(qualifiedClassName));
            qualifiedClassName.EnsureQualifiedName(nameof(qualifiedClassName));
            QualifiedClassName = qualifiedClassName;

            // Interface name is optional but must be valid if present
            if (!string.IsNullOrWhiteSpace(qualifiedInterfaceName))
            {
                qualifiedInterfaceName.EnsureQualifiedName(nameof(qualifiedInterfaceName));
            }
            QualifiedInterfaceName = qualifiedInterfaceName;

            // Modifiers
            IsArray = isArray;
            IsNullable = isNullable;
            ElementIsNullable = elementIsNullable;

            InterfaceIsArray = interfaceIsArray;
            InterfaceIsNullable = interfaceIsNullable;
            InterfaceElementIsNullable = interfaceElementIsNullable;

            // Collections
            Assignments = (assignments ?? Array.Empty<NamedInstanceAssignmentDto>()).ToList().AsReadOnly();
            Elements = (elements ?? Array.Empty<NamedInstanceElementDto>()).ToList().AsReadOnly();

            if (Assignments.Count > 0 && Elements.Count > 0)
                throw new ArgumentException("Named instance cannot specify both Assignments and Elements.");

            // Provenance
            if (provenanceStack is null || ProvenanceStackEntriesEmpty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));
            ProvenanceStack = provenanceStack;

            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }

        private static bool ProvenanceStackEntriesEmpty(ProvenanceStack stack) =>
            stack.Entries == null || stack.Entries.Count == 0;
    }
}

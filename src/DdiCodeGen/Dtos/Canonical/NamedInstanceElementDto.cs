namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing an element in a named instance array.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed class NamedInstanceElementDto : IHaveProvenance
    {
        public string? AssignmentValue { get; }
        public string? AssignmentNamedInstanceName { get; }
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public NamedInstanceElementDto(
            string? assignmentValue,
            string? assignmentNamedInstanceName,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            if (assignmentValue != null && string.IsNullOrWhiteSpace(assignmentValue))
                throw new ArgumentException("Value cannot be empty if provided.", nameof(assignmentValue));
            AssignmentValue = assignmentValue;

            if (!string.IsNullOrWhiteSpace(assignmentNamedInstanceName))
            {
                assignmentNamedInstanceName.EnsureValidIdentifier(nameof(assignmentNamedInstanceName));
                if (!assignmentNamedInstanceName.IsPascalCase())
                    throw new ArgumentException("NamedInstanceName must follow PascalCase convention.", nameof(assignmentNamedInstanceName));
            }
            AssignmentNamedInstanceName = assignmentNamedInstanceName;

            if (!string.IsNullOrWhiteSpace(assignmentValue) && !string.IsNullOrWhiteSpace(assignmentNamedInstanceName))
                throw new ArgumentException("Element cannot specify both Value and NamedInstanceName.");

            if (provenanceStack is null || provenance_stack_entries_empty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));
            ProvenanceStack = provenanceStack;

            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }

        private static bool provenance_stack_entries_empty(ProvenanceStack stack) => stack.Entries == null || stack.Entries.Count == 0;
    }
}

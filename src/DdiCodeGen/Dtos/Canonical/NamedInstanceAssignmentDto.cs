namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing an assignment for a named instance initializer.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed record NamedInstanceAssignmentDto : IHaveProvenance
    {
        public string AssignmentParameterName { get; }
        public string? AssignmentValue { get; }
        public string? AssignmentNamedInstanceName { get; }
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public NamedInstanceAssignmentDto(
            string assignmentParameterName,
            string? assignmentValue,
            string? assignmentNamedInstanceName,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            assignmentParameterName.EnsureValidIdentifier(nameof(assignmentParameterName));
            AssignmentParameterName = assignmentParameterName;

            if (assignmentValue != null && string.IsNullOrWhiteSpace(assignmentValue))
                throw new ArgumentException("AssignmentValue cannot be empty if provided.", nameof(assignmentValue));
            AssignmentValue = assignmentValue;

            if (!string.IsNullOrWhiteSpace(assignmentNamedInstanceName))
            {
                assignmentNamedInstanceName.EnsureValidIdentifier(nameof(assignmentNamedInstanceName));
                if (!assignmentNamedInstanceName.IsPascalCase())
                    throw new ArgumentException("NamedInstanceName must follow PascalCase convention.", nameof(assignmentNamedInstanceName));
            }
            AssignmentNamedInstanceName = assignmentNamedInstanceName;

            if (!string.IsNullOrWhiteSpace(assignmentValue) && !string.IsNullOrWhiteSpace(assignmentNamedInstanceName))
                throw new ArgumentException("Assignment cannot specify both AssignmentValue and NamedInstanceName.");

            if (provenanceStack is null || provenance_stack_entries_empty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));
            ProvenanceStack = provenanceStack;

            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }

        private static bool provenance_stack_entries_empty(ProvenanceStack stack) => stack.Entries == null || stack.Entries.Count == 0;
    }
}

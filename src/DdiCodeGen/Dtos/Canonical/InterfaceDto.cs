using System;
using System.Collections.Generic;
using DdiCodeGen.Validation;

namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing an interface declaration.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed record InterfaceDto
    {
        public string InterfaceName { get; }
        public string QualifiedInterfaceName { get; }
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public InterfaceDto(
            string interfaceName,
            string qualifiedInterfaceName,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            interfaceName.EnsureValidIdentifier(nameof(interfaceName));
            if (!interfaceName.IsInterfaceName())
                throw new ArgumentException("Interface name must start with 'I' followed by uppercase.", nameof(interfaceName));
            InterfaceName = interfaceName;

            qualifiedInterfaceName.EnsureQualifiedName(nameof(qualifiedInterfaceName));
            QualifiedInterfaceName = qualifiedInterfaceName;

            if (provenanceStack is null || provenance_stack_entries_empty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));
            ProvenanceStack = provenanceStack;

            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }

        private static bool provenance_stack_entries_empty(ProvenanceStack stack) => stack.Entries == null || stack.Entries.Count == 0;
    }
}

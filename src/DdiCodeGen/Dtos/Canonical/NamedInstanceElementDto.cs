using System;
using System.Collections.Generic;
using System.Linq;
using DdiCodeGen.Validation;

namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing an element in a named instance array.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed class NamedInstanceElementDto
    {
        public string? Value { get; }
        public string? NamedInstanceName { get; }
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public NamedInstanceElementDto(
            string? value,
            string? namedInstanceName,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            if (value != null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty if provided.", nameof(value));
            Value = value;

            if (!string.IsNullOrWhiteSpace(namedInstanceName))
            {
                namedInstanceName.EnsureValidIdentifier(nameof(namedInstanceName));
                if (!namedInstanceName.IsPascalCase())
                    throw new ArgumentException("NamedInstanceName must follow PascalCase convention.", nameof(namedInstanceName));
            }
            NamedInstanceName = namedInstanceName;

            if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(namedInstanceName))
                throw new ArgumentException("Element cannot specify both Value and NamedInstanceName.");

            if (provenanceStack is null || provenance_stack_entries_empty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));
            ProvenanceStack = provenanceStack;

            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }

        private static bool provenance_stack_entries_empty(ProvenanceStack stack) => stack.Entries == null || stack.Entries.Count == 0;
    }
}

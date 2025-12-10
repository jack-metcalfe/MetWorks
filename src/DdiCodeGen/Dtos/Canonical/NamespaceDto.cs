using System;
using System.Collections.Generic;
using System.Linq;
using DdiCodeGen.Validation;

namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing a namespace and its declared types.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed record NamespaceDto
    {
        public string NamespaceName { get; }
        public IReadOnlyList<InterfaceDto> Interfaces { get; }
        public IReadOnlyList<ClassDto> Classes { get; }
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public NamespaceDto(
            string namespaceName,
            IReadOnlyList<InterfaceDto>? interfaces,
            IReadOnlyList<ClassDto>? classes,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            if (!namespaceName.IsValidNamespace())
                throw new ArgumentException("Namespace must be a valid dot-separated namespace with valid identifier segments.", nameof(namespaceName));
            NamespaceName = namespaceName;

            Interfaces = (interfaces ?? Array.Empty<InterfaceDto>()).ToList().AsReadOnly();
            Classes = (classes ?? Array.Empty<ClassDto>()).ToList().AsReadOnly();

            if (provenanceStack is null || provenance_stack_entries_empty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));
            ProvenanceStack = provenanceStack;

            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }

        // local helper to avoid repeated property access in constructor check
        private static bool provenance_stack_entries_empty(ProvenanceStack stack) => stack.Entries == null || stack.Entries.Count == 0;
    }
}

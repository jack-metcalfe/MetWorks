using System;
using System.Collections.Generic;
using System.Linq;
using DdiCodeGen.Validation;

namespace DdiCodeGen.Dtos.Canonical
{
    /// <summary>
    /// Canonical DTO representing a class declaration.
    /// Identifiers are guaranteed C#-safe at this boundary.
    /// </summary>
    public sealed record ClassDto
    {
        public string ClassName { get; }
        public string ShortName { get; }
        public string InvokerKey { get; }
        public string QualifiedClassName { get; }
        public string? QualifiedInterfaceName { get; }
        public string ReturnTypeQualifiedName { get; }
        public IReadOnlyList<ParameterDto> InitializerParameters { get; }
        public ProvenanceStack ProvenanceStack { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public ClassDto(
            string className,
            string shortName,
            string invokerKey,
            string qualifiedClassName,
            string? qualifiedInterfaceName,
            string returnTypeQualifiedName,
            IReadOnlyList<ParameterDto> initializerParameters,
            ProvenanceStack provenanceStack,
            IReadOnlyList<Diagnostic>? diagnostics)
        {
            // Local invariants (class-level)
            className.EnsureValidIdentifier(nameof(className));
            shortName.EnsureValidIdentifier(nameof(shortName));
            invokerKey.EnsureValidIdentifier(nameof(invokerKey));

            if (string.IsNullOrWhiteSpace(qualifiedClassName) || !qualifiedClassName.IsQualifiedName())
                throw new ArgumentException("qualifiedClassName must be a valid qualified name.", nameof(qualifiedClassName));

            if (string.IsNullOrWhiteSpace(returnTypeQualifiedName) || !returnTypeQualifiedName.IsQualifiedName())
                throw new ArgumentException("returnTypeQualifiedName must be a valid qualified name.", nameof(returnTypeQualifiedName));

            if (provenanceStack is null || provenance_stack_entries_empty(provenanceStack))
                throw new ArgumentException("Provenance stack must be provided and non-empty.", nameof(provenanceStack));

            ClassName = className;
            ShortName = shortName;
            InvokerKey = invokerKey;
            QualifiedClassName = qualifiedClassName;
            QualifiedInterfaceName = qualifiedInterfaceName;
            ReturnTypeQualifiedName = returnTypeQualifiedName;
            InitializerParameters = initializerParameters ?? Array.Empty<ParameterDto>();
            ProvenanceStack = provenanceStack;
            Diagnostics = (diagnostics ?? Array.Empty<Diagnostic>()).ToList().AsReadOnly();
        }

        private static bool provenance_stack_entries_empty(ProvenanceStack stack) => stack.Entries == null || stack.Entries.Count == 0;
    }
}

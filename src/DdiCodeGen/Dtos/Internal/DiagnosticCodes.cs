namespace DdiCodeGen.Dtos.Internal
{
    public enum DiagnosticCode
    {
        // Provenance (PROVxxx)
        ProvenanceMissingEntries,
        ProvenanceMissingLogicalPath,
        ProvenanceInvalidVersion,

        // Namespaces (NSxxx)
        NamespaceMissingName,
        NamespaceInvalidSegment,        // segment violates identifier rules
        NamespaceEmptySegment,          // e.g., "Company..Product"
        NamespaceReservedSegment,       // segment is a C# keyword

        // Identifiers (IDxxx)
        InvalidIdentifier,              // general identifier pattern violation
        ReservedIdentifier,             // identifier is a C# keyword/contextual keyword
        DuplicateTypeIdentifier,        // same (namespace, name) used by multiple types
        DuplicateInstanceIdentifier,    // duplicate named instance in same namespace

        // Interfaces (IFACExxx)
        InterfaceMissingName,
        InterfaceMissingQualifiedName,

        // Classes (CLASSxxx)
        ClassMissingName,
        ClassBothClassAndInterfaceSet,
        ClassNeitherClassNorInterfaceSet,
        ClassMissingParameters,

        // Parameters (PARAMxxx)
        ParameterMissingName,
        ParameterMissingQualifiedClass,
        ParameterMissingQualifiedInterface,
        ParameterBothClassAndInterface,
        ParameterMissingClassOrInterface,
        ParameterMustBeInterfaceForNonPrimitive,

        // Named Instances (NIxxx)
        NamedInstanceMissingName,
        NamedInstanceMissingQualifiedClass,
        NamedInstanceBothAssignmentsAndElementsSet,
        NamedInstanceDuplicateName,      // mirrors ID004 but scoped to instances
        NamedInstanceMissing,               // referenced named instance not found
        NamedInstanceMissingOrNotExposingInterface, // referenced named instance not found or does not expose required interface

        // Assignments (ASSIGNxxx)
        AssignmentMissingParameterName,
        AssignmentMissingValueOrInstance,

        // Elements (ELEMxxx)
        ElementMissingValueOrInstance,
        // CodeGen config (CODEGENxxx)
        CodeGenMissingRegistryClass,
        CodeGenMissingGeneratedPath,
        CodeGenMissingNamespace,
        CodeGenMissingInitializer,

        // Generator tokens/templates (GENxxx)
        UnrecognizedToken,
        MissingTemplate,                // requested template not found
        MissingPlaceholderValue,        // placeholder lacked a DTO value
        DuplicateInvokerKey,              // same invoker key used multiple times
        DependencyOrderViolation,        // generator dependency ordering is invalid
        TypeRefInvalid,               // type reference string is malformed
        DtoValidationException,               // exception thrown during DTO validation
        TemplateUnresolvedPlaceholder      // template placeholder could not be resolved    
    }
    public static class DiagnosticCodeInfo
    {
        private static readonly IReadOnlyDictionary<DiagnosticCode, DiagnosticSeverity> _severityMap =
            new Dictionary<DiagnosticCode, DiagnosticSeverity>
            {
        // Provenance (PROVxxx)
        { DiagnosticCode.ProvenanceMissingEntries, DiagnosticSeverity.Error },
        { DiagnosticCode.ProvenanceMissingLogicalPath, DiagnosticSeverity.Error },
        { DiagnosticCode.ProvenanceInvalidVersion, DiagnosticSeverity.Error },
        { DiagnosticCode.ProvenanceMissingLogicalPath, DiagnosticSeverity.Error },
        { DiagnosticCode.ProvenanceInvalidVersion, DiagnosticSeverity.Error },        
        // Namespaces (NSxxx)
        { DiagnosticCode.NamespaceMissingName, DiagnosticSeverity.Error },
        { DiagnosticCode.NamespaceInvalidSegment, DiagnosticSeverity.Error },
        { DiagnosticCode.NamespaceEmptySegment, DiagnosticSeverity.Error },
        { DiagnosticCode.NamespaceReservedSegment, DiagnosticSeverity.Warning },
        { DiagnosticCode.NamespaceInvalidSegment, DiagnosticSeverity.Error },
        { DiagnosticCode.NamespaceEmptySegment, DiagnosticSeverity.Error },
        { DiagnosticCode.NamespaceReservedSegment, DiagnosticSeverity.Warning },

        // Identifiers (IDxxx)
        { DiagnosticCode.InvalidIdentifier, DiagnosticSeverity.Error },
        { DiagnosticCode.ReservedIdentifier, DiagnosticSeverity.Warning },
        { DiagnosticCode.DuplicateTypeIdentifier, DiagnosticSeverity.Error },
        { DiagnosticCode.DuplicateInstanceIdentifier, DiagnosticSeverity.Error },
        { DiagnosticCode.ReservedIdentifier, DiagnosticSeverity.Warning },
        { DiagnosticCode.DuplicateTypeIdentifier, DiagnosticSeverity.Error },
        { DiagnosticCode.DuplicateInstanceIdentifier, DiagnosticSeverity.Error },

        // Interfaces (IFACExxx)
        { DiagnosticCode.InterfaceMissingName, DiagnosticSeverity.Error },
        { DiagnosticCode.InterfaceMissingQualifiedName, DiagnosticSeverity.Error },

        // Classes (CLASSxxx)
        { DiagnosticCode.ClassMissingName, DiagnosticSeverity.Error },
        { DiagnosticCode.ClassBothClassAndInterfaceSet, DiagnosticSeverity.Error },
        { DiagnosticCode.ClassNeitherClassNorInterfaceSet, DiagnosticSeverity.Error },
        { DiagnosticCode.ClassMissingParameters, DiagnosticSeverity.Error },

        // Parameters (PARAMxxx)
        { DiagnosticCode.ParameterMissingName, DiagnosticSeverity.Error },
        { DiagnosticCode.ParameterMissingQualifiedClass, DiagnosticSeverity.Error },
        { DiagnosticCode.ParameterMissingQualifiedInterface, DiagnosticSeverity.Error },
        { DiagnosticCode.ParameterBothClassAndInterface, DiagnosticSeverity.Error },
        { DiagnosticCode.ParameterMissingClassOrInterface, DiagnosticSeverity.Error },
        { DiagnosticCode.ParameterMustBeInterfaceForNonPrimitive, DiagnosticSeverity.Error },

        // Named Instances (NIxxx)
        { DiagnosticCode.NamedInstanceMissingName, DiagnosticSeverity.Error },
        { DiagnosticCode.NamedInstanceMissingQualifiedClass, DiagnosticSeverity.Error },
        { DiagnosticCode.NamedInstanceBothAssignmentsAndElementsSet, DiagnosticSeverity.Error },
        { DiagnosticCode.NamedInstanceDuplicateName, DiagnosticSeverity.Error },
        { DiagnosticCode.NamedInstanceMissing, DiagnosticSeverity.Error },
        { DiagnosticCode.NamedInstanceMissingOrNotExposingInterface, DiagnosticSeverity.Error },

        // Assignments (ASSIGNxxx)
        { DiagnosticCode.AssignmentMissingParameterName, DiagnosticSeverity.Error },
        { DiagnosticCode.AssignmentMissingValueOrInstance, DiagnosticSeverity.Error },

        // Elements (ELEMxxx)
        { DiagnosticCode.ElementMissingValueOrInstance, DiagnosticSeverity.Error },
        // CodeGen config (CODEGENxxx)
        { DiagnosticCode.CodeGenMissingRegistryClass, DiagnosticSeverity.Error },
        { DiagnosticCode.CodeGenMissingGeneratedPath, DiagnosticSeverity.Error },
        { DiagnosticCode.CodeGenMissingNamespace, DiagnosticSeverity.Error },
        { DiagnosticCode.CodeGenMissingInitializer, DiagnosticSeverity.Error },

        // Generator tokens/templates (GENxxx)
        { DiagnosticCode.UnrecognizedToken, DiagnosticSeverity.Error },
        { DiagnosticCode.MissingTemplate, DiagnosticSeverity.Error },
        { DiagnosticCode.MissingPlaceholderValue, DiagnosticSeverity.Error },
        { DiagnosticCode.DuplicateInvokerKey, DiagnosticSeverity.Error },
        { DiagnosticCode.DependencyOrderViolation, DiagnosticSeverity.Error },
        { DiagnosticCode.TypeRefInvalid, DiagnosticSeverity.Error },
        { DiagnosticCode.DtoValidationException, DiagnosticSeverity.Error },
        { DiagnosticCode.TemplateUnresolvedPlaceholder, DiagnosticSeverity.Error },
        { DiagnosticCode.MissingTemplate, DiagnosticSeverity.Error },
        { DiagnosticCode.MissingPlaceholderValue, DiagnosticSeverity.Error },
        { DiagnosticCode.DuplicateInvokerKey, DiagnosticSeverity.Error },
        { DiagnosticCode.DependencyOrderViolation, DiagnosticSeverity.Error },
        { DiagnosticCode.TypeRefInvalid, DiagnosticSeverity.Error },
        { DiagnosticCode.DtoValidationException, DiagnosticSeverity.Error },
        { DiagnosticCode.TemplateUnresolvedPlaceholder, DiagnosticSeverity.Error },
        { DiagnosticCode.MissingTemplate, DiagnosticSeverity.Error },
        { DiagnosticCode.DuplicateInvokerKey, DiagnosticSeverity.Error },
        { DiagnosticCode.DependencyOrderViolation, DiagnosticSeverity.Error },
        { DiagnosticCode.TypeRefInvalid, DiagnosticSeverity.Error },
        { DiagnosticCode.DtoValidationException, DiagnosticSeverity.Error },
        { DiagnosticCode.TemplateUnresolvedPlaceholder, DiagnosticSeverity.Error }
            };

        public static DiagnosticSeverity GetSeverity(this DiagnosticCode code)
            => _severityMap.TryGetValue(code, out var s) ? s : DiagnosticSeverity.Error;

        public static bool HasMapping(this DiagnosticCode code) => _severityMap.ContainsKey(code);
    }
}

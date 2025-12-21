Diagnostic Taxonomy
Parser	DiagnosticCode(s)	Meaning
ParseModel	NamespaceMissingName, NamedInstanceMissingName	Missing required namespaces or namedInstances sections at root
ParseCodeGen	InvalidIdentifier, NamespaceInvalidSegment	Bad registry class, initializer name, or namespace; invalid packageReferences entries
CreateMissingCodeGen	CodeGenMissingRegistryClass	Entire codeGen section absent
ParseNamespace	NamespaceMissingName, InvalidIdentifier, NamespaceNameMustBeSimple, NamespaceInvalidNode	Namespace name missing/invalid, or non‑mapping node in sequence
ParseInterface	InterfaceMissingName, InvalidIdentifier, InterfaceInvalidNode	Interface name missing/invalid, or node not scalar/mapping
ParseClass	ClassMissingName, InvalidIdentifier, ClassInvalidNode, InterfaceMissingQualifiedName	Class name missing/invalid, interface name invalid, or non‑mapping node
ParseParameter	ParameterMissingQualifiedClass, TypeRefInvalid, InvalidIdentifier	QualifiedClassName missing/invalid, bad type reference, or bad parameter name
ParseNamedInstance	NamedInstanceMissingName, InvalidIdentifier, NamedInstanceMissingQualifiedClass, TypeRefInvalid, InterfaceMissingQualifiedName, NamedInstanceBothAssignmentsAndElementsSet, NamedInstanceInvalidNode	NamedInstance name/class missing/invalid, bad type ref, both assignments+elements set, or non‑mapping node
ParseAssignment	AssignmentMissingParameterName, InvalidIdentifier, AssignmentInvalidNode	ParameterName missing/invalid, AssignedNamedInstance invalid, or non‑mapping node
ParseElement	ElementMissingValue, ElementMissingNamedInstance, InvalidIdentifier, ElementInvalidNode	AssignedValue missing, AssignedNamedInstance missing/invalid, or non‑mapping node
Why this matters
Coverage check: Every parser has explicit diagnostic codes, no silent failures.

Symmetry: Each entity (namespace, class, interface, parameter, instance, assignment, element) has its own diagnostic family.

Readability: Future contributors can scan this table to understand which parser emits which codes.

Debugging: When a diagnostic appears, you can instantly trace it back to the responsible parser.

Would you like me to also sketch a diagnostic severity mapping (Info/Warning/Error) so you can decide which codes should halt generation vs which should just annotate provenance?
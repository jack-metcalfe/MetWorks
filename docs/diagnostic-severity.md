Diagnostic Severity Mapping
DiagnosticCode	Suggested Severity	Rationale
CodeGenMissingRegistryClass	Error	Root codeGen section missing; generation cannot proceed without registry class.
NamespaceMissingName	Error	Required namespaceName missing; invalid model structure.
NamespaceNameMustBeSimple	Error	Namespace must be a simple identifier; invalid grammar.
NamespaceInvalidNode	Error	Non‑mapping node in namespaces sequence; cannot parse.
NamespaceInvalidSegment	Error	Namespace string contains invalid segments; breaks grammar.
InterfaceMissingName	Error	Required interfaceName missing; invalid interface definition.
InterfaceInvalidNode	Error	Non‑scalar/mapping node in interfaces sequence; cannot parse.
InterfaceMissingQualifiedName	Error	Qualified interface name invalid; breaks grammar.
ClassMissingName	Error	Required className missing; invalid class definition.
ClassInvalidNode	Error	Non‑mapping node in classes sequence; cannot parse.
InvalidIdentifier	Error	Any identifier failing SimpleName/QualifiedName rules; grammar violation.
ParameterMissingQualifiedClass	Error	Parameter missing required type reference; invalid parameter definition.
TypeRefInvalid	Error	Type reference fails parsing; grammar violation.
NamedInstanceMissingName	Error	Required namedInstanceName missing; invalid instance definition.
NamedInstanceMissingQualifiedClass	Error	Required qualifiedClassName missing/invalid; invalid instance definition.
NamedInstanceBothAssignmentsAndElementsSet	Error	Instance has both assignments and elements; semantic violation.
NamedInstanceInvalidNode	Error	Non‑mapping node in namedInstances sequence; cannot parse.
AssignmentMissingParameterName	Error	Required parameterName missing; invalid assignment.
AssignmentInvalidNode	Error	Non‑mapping node in assignments sequence; cannot parse.
ElementMissingValue	Error	Required assignedValue missing; invalid element.
ElementMissingNamedInstance	Error	Required assignedNamedInstance missing; invalid element.
ElementInvalidNode	Error	Non‑mapping node in elements sequence; cannot parse.
Notes
I’ve marked almost everything as Error because these are structural/grammar violations that prevent deterministic parsing.

If you want softer handling, you could downgrade some to Warning:

InvalidIdentifier → Warning if you want to allow generation with placeholders.

NamespaceInvalidSegment → Warning if you want to allow partial namespace parsing.

PackageReferences issues (empty id, unsupported node type) → Warning, since codegen can continue without them.

Info severity could be used for non‑fatal annotations, e.g. “element ignored” or “assignment skipped,” but your current taxonomy doesn’t have many purely informational codes.

This severity mapping gives you a clear policy: Errors stop generation, Warnings allow generation but flag issues, Info annotates provenance.

Would you like me to now sketch a severity policy document (like a one‑pager for contributors) that explains how to interpret and act on each severity level in practice? That would make onboarding even smoother.
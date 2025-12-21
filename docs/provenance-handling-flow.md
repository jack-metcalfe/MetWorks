Provenance Flow
Level	Provenance source	LogicalPath example	Notes
Root (ParseModel)	root mapping node		Captures entire YAML file origin
CodeGen (ParseCodeGen / CreateMissingCodeGen)	codeGen mapping node or synthetic origin	codeGen	Always produces a provenance stack, even if missing
Namespace (ParseNamespace)	namespace mapping node	namespaces[0]	Each namespace gets its own stack
Interface (ParseInterface)	scalar or mapping node	namespaces[0].interfaces[0]	Handles both scalar and mapping forms
Class (ParseClass)	class mapping node	namespaces[0].classes[0]	Includes provenance for initializer parameters
Parameter (ParseParameter)	parameter mapping node	namespaces[0].classes[0].initializerParameters[0]	Each parameter stack traces back to its node
NamedInstance (ParseNamedInstance)	namedInstance mapping node	namedInstances[0]	Includes provenance for assignments and elements
Assignment (ParseAssignment)	assignment mapping node	namedInstances[0].assignments[0]	Invalid nodes still get provenance
Element (ParseElement)	element mapping node	namedInstances[0].elements[0]	Same provenance rules as assignments
How it works
Every parser calls MakeProvStack(...) with (node, sourcePath, logicalPath).

LogicalPath strings are built consistently (namespaces[0].classes[0].initializerParameters[0]).

Synthetic provenance is created for missing sections (e.g. CreateMissingCodeGen).

Invalid nodes (non‑mapping children) still get provenance, so diagnostics can point to the exact YAML location.

This symmetry means provenance is predictable, traceable, and complete across the entire hierarchy. It’s a huge win for debugging and onboarding — every DTO carries a breadcrumb trail back to its YAML origin.

Would you like me to now sketch a diagnostic taxonomy table (mapping each DiagnosticCode to the parser that emits it), so you can see coverage and avoid overlaps?
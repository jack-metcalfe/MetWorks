Parser Stack Overview
Level	Single‑node parser	Sequence helper	Key rules enforced
Root	ParseModel	–	Validates root keys, aggregates diagnostics, ties together codegen, namespaces, instances
CodeGen	ParseCodeGen	–	RegistryClassName = SimpleName, NamespaceName = QualifiedName, InitializerName = SimpleName, parses packageReferences
Namespace	ParseNamespace	–	NamespaceName = SimpleName, aggregates interfaces and classes
Interface	ParseInterface	GetInterfaceTokens	InterfaceName = SimpleName, scalar or mapping node allowed
Class	ParseClass	–	ClassName = SimpleName, QualifiedInterfaceName = QualifiedName, parses initializerParameters
Parameter	ParseParameter	GetParameterTokens	QualifiedClassName / QualifiedInterfaceName required, parsed flags enforced
NamedInstance	ParseNamedInstance	–	QualifiedClassName = QualifiedName (required), QualifiedInterfaceName = QualifiedName (optional), exclusivity check between assignments/elements
Assignment	ParseAssignment	GetAssignmentTokens	AssignmentParameterName = SimpleName, AssignedNamedInstance = SimpleName
Element	ParseElement	GetElementTokens	AssignedNamedInstance = SimpleName, AssignedValue required
Why this matters
Symmetry: Every DTO type now has a predictable pattern — single‑node parser plus sequence helper.

Consistency: SimpleName vs QualifiedName rules are enforced uniformly.

Readability: Future contributors can scan this stack and instantly know where to look for parsing logic.

Diagnostics: Each parser emits explicit, localized diagnostics, aggregated at higher levels.
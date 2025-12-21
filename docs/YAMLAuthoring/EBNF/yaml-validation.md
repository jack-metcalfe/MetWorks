Validator Checklist
CodeGen block

All required members: generatedCodePath, registryClassName, namespaceName, initializerName, packageReferences must be present.

PackageReferences: may be empty ([]), but if non‑empty, each entry must have both id and version (version may be null).

Namespace block

namespaceName: required.

interfaces: always present; may be empty.

classes: always present; may be empty.

Class declarations

className: required.

qualifiedInterfaceName: required, may be null.

initializerParameters: required, may be empty.

If empty → class is parameterless.

If non‑empty → each parameter must specify exactly one of qualifiedClassName or qualifiedInterfaceName (the other must be null).

Parameters

parameterName: required.

qualifiedClassName: may be null or a valid TypeRef.

qualifiedInterfaceName: may be null or a valid TypeRef.

Disambiguation: exactly one side (class or interface) must be non‑null.

Named instances

namedInstanceName: required.

qualifiedClassName: required, must be a valid TypeRef.

assignments: required, may be empty.

If the referenced class has empty initializerParameters, then assignments must also be empty.

If the class has parameters, assignments must bind only to those parameters.

elements: required, may be empty.

If present, each element must have both assignedValue and assignedNamedInstance.

Assignments

parameterName: must match a declared parameter in the class.

assignedValue: may be null or a string.

assignedNamedInstance: may be null or a valid instance name.

Elements

assignedValue: required string.

assignedNamedInstance: required identifier.

Type references

Allowed forms: Foo, Foo?, Foo[], Foo[]?.

Disallowed form: nullable array of nullable elements (e.g. Foo?[]?).

Modifiers: expressed only in C# token form (?, [], []?), never as booleans in YAML.

This checklist gives you explicit guardrails for your transformer: it can raise diagnostics whenever a fixture violates these rules.

Would you like me to also sketch out a diagnostic message template set (e.g. “Missing required key: initializerName”, “Assignments not allowed for parameterless class”) so your loader can produce consistent, actionable errors?
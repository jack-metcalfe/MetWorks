Area	Raw	Canonical	Difference
Provenance	Nullable fields everywhere	Required fields, adds Latest + MinVersion	Canonical stricter, adds helpers
Diagnostics	Not present	Every DTO has Diagnostics	Canonical introduces diagnostics
Top‑Level Model	RawModelDto with nullable collections	CanonicalModelDto with required collections + diagnostics	Canonical enforces non‑null, adds diagnostics
CodeGen	Minimal fields, no provenance/diagnostics	All required, adds provenance + diagnostics	Canonical enforces + validates
Namespaces	Interfaces = list of strings	Interfaces = structured InterfaceDto	Canonical expands interfaces
Classes	Short ClassName, optional QualifiedInterfaceName	Adds QualifiedClassName, renames Parameters → InitializerParameters	Canonical derives qualified names
Parameters	Class vs interface tokens only	Adds IsValid + diagnostics	Canonical validates
Named Instances	Name, class, assignments, elements	Same, but adds diagnostics + provenance	Canonical enforces exclusivity + validation
Assignments	ParameterName, AssignedValue, AssignedNamedInstance	AssignmentParameterName, Value, NamedInstanceName	DDR‑012: Canonical naming alignment
Elements	Value, NamedInstanceName	Same, but adds diagnostics + provenance	Canonical validates
Accessor DTO	Not present	Adds NamedInstanceAccessorDto	Canonical introduces accessor concept
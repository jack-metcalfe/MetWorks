Transform(RawModelDto)
 ├─ TransformCodeGen(RawCodeGenDto) → CanonicalCodeGenDto
 │
 ├─ TransformNamespace(RawNamespaceDto) → NamespaceDto
 │    ├─ TransformInterface(RawInterfaceDto) → InterfaceDto
 │    └─ TransformClass(RawClassDto) → ClassDto
 │         └─ TransformParameter(RawParameterDto) → ParameterDto
 │
 └─ TransformNamedInstance(RawNamedInstanceDto) → NamedInstanceDto
      ├─ TransformAssignment(RawNamedInstanceAssignmentDto) → NamedInstanceAssignmentDto
      └─ TransformElement(RawNamedInstanceElementDto) → NamedInstanceElementDto

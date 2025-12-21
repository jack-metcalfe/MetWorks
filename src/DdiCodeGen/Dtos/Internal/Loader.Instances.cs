namespace DdiCodeGen.Dtos.Internal
{
    internal sealed partial class Loader
    {
        // --- YamlRawModelLoader.Instances.cs (excerpt with updated ParseNamedInstance) ---

        private RawNamedInstanceDto ParseNamedInstance(
            YamlMappingNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(
                node,
                typeof(RawNamedInstanceDto),
                logicalPath,
                sourcePath).ToList();

            // --- NamedInstanceName ---
            var name = GetScalar(node, "namedInstanceName");
            if (string.IsNullOrWhiteSpace(name))
            {
                DiagnosticsHelper.Add(
                    list: diagnostics,
                    code: DiagnosticCode.NamedInstanceMissingName,
                    message: $"Missing 'namedInstanceName' in {logicalPath}.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.namedInstanceName")
                );
                name = "<missing.instance>";
            }
            else if (!name.IsValidIdentifier())
            {
                DiagnosticsHelper.Add(
                    list: diagnostics,
                    code: DiagnosticCode.InvalidIdentifier,
                    message: $"NamedInstanceName '{name}' is not a valid identifier.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.namedInstanceName")
                );
            }

            // --- QualifiedClassName (required, QualifiedName) ---
            var qualifiedClass = GetScalar(node, "qualifiedClassName");
            string? baseClass = null;
            bool classIsArray = false, classIsContainerNullable = false,
                 classIsElementNullable = false;

            if (string.IsNullOrWhiteSpace(qualifiedClass))
            {
                DiagnosticsHelper.Add(
                    list: diagnostics,
                    code: DiagnosticCode.NamedInstanceMissingQualifiedClass,
                    message: $"Missing 'qualifiedClassName' in {logicalPath}.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName")
                );
                qualifiedClass = "<missing.qualifiedClass>";
            }
            else
            {
                if (!qualifiedClass.TryParseTypeRef(
                    out baseClass,
                    out classIsArray,
                    out classIsContainerNullable,
                    out classIsElementNullable))
                {

                    DiagnosticsHelper.Add(
                        list: diagnostics,
                        code: DiagnosticCode.TypeRefInvalid,
                        message: $"Invalid type reference '{qualifiedClass}' in " +
                        $"{logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', " +
                        "'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside " +
                        "arrays (e.g., 'Ns.Type?[]') are not supported.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName")
                    );

                    baseClass = null;
                    classIsArray = false;
                    classIsContainerNullable = false;
                    classIsElementNullable = false;
                }
                else if (string.IsNullOrWhiteSpace(baseClass) ||
                         !baseClass.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(
                        list: diagnostics,
                        code: DiagnosticCode.NamedInstanceMissingQualifiedClass,
                        message: $"QualifiedClassName '{qualifiedClass}' is not a valid " +
                        "qualified name after parsing.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath,
                            $"{logicalPath}.qualifiedClassName"));
                }
            }

            // --- QualifiedInterfaceName (optional, QualifiedName or "null") ---
            var exposeInterface = GetScalar(node, "qualifiedInterfaceName");
            if (string.Equals(
                exposeInterface,
                "null",
                StringComparison.OrdinalIgnoreCase))
            {
                exposeInterface = null;
            }

            string? baseIface = null;
            bool ifaceIsArray = false, ifaceIsContainerNullable = false,
                 ifaceIsElementNullable = false;

            if (!string.IsNullOrWhiteSpace(exposeInterface))
            {
                if (!exposeInterface.TryParseTypeRef(
                    out baseIface,
                    out ifaceIsArray,
                    out ifaceIsContainerNullable,
                    out ifaceIsElementNullable))
                {

                    DiagnosticsHelper.Add(
                        list: diagnostics,
                        code: DiagnosticCode.TypeRefInvalid,
                        message: $"Invalid type reference '{exposeInterface}' in " +
                        $"{logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', " +
                        "'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside " +
                        "arrays (e.g., 'Ns.Type?[]') are not supported.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath,
                            $"{logicalPath}.qualifiedInterfaceName"));

                    baseIface = null;
                    ifaceIsArray = false;
                    ifaceIsContainerNullable = false;
                    ifaceIsElementNullable = false;
                }
                else if (string.IsNullOrWhiteSpace(baseIface) ||
                         !baseIface.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(
                        list: diagnostics,
                        code: DiagnosticCode.InterfaceMissingQualifiedName,
                        message: $"QualifiedInterfaceName '{exposeInterface}' is not a " +
                        "valid qualified name after parsing.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath,
                            $"{logicalPath}.qualifiedInterfaceName"));
                }
            }

            // --- Assignments ---
            var assignmentsSeq = GetChildSequence(node, "assignments");
            var assignments = new List<RawNamedInstanceAssignmentDto>();
            if (assignmentsSeq != null)
            {
                for (int i = 0; i < assignmentsSeq.Children.Count; i++)
                {
                    var childLogical = $"{logicalPath}.assignments[{i}]";
                    var child = assignmentsSeq.Children[i];

                    if (child is YamlMappingNode map)
                    {
                        assignments.Add(
                            ParseAssignment(map, sourcePath, childLogical));
                    }
                    else
                    {
                        var prov = MakeProvStack(child, sourcePath, childLogical);
                        var diags = new List<Diagnostic>();
                        DiagnosticsHelper.Add(
                            list: diags,
                            code: DiagnosticCode.AssignmentInvalidNode,
                            message: $"Assignment at {childLogical} must be a mapping node.",
                            provenance: ProvenanceHelper.MakeProvenance(prov),
                            fallbackLocation: BuildLocation(sourcePath, childLogical)
                        );

                        assignments.Add(new RawNamedInstanceAssignmentDto(
                            AssignmentParameterName: "<invalid.param>",
                            AssignmentValue: null,
                            AssignmentNamedInstanceName: null,
                            ProvenanceStack: prov,
                            Diagnostics: diags.AsReadOnly()
                        ));
                    }
                }
            }

            // --- Elements ---
            var elementsSeq = GetChildSequence(node, "elements");
            var elements = new List<RawNamedInstanceElementDto>();
            if (elementsSeq != null)
            {
                for (int i = 0; i < elementsSeq.Children.Count; i++)
                {
                    var childLogical = $"{logicalPath}.elements[{i}]";
                    var child = elementsSeq.Children[i];

                    if (child is YamlMappingNode map)
                    {
                        elements.Add(ParseElement(map, sourcePath, childLogical));
                    }
                    else
                    {
                        var prov = MakeProvStack(child, sourcePath, childLogical);
                        var diags = new List<Diagnostic>();
                        DiagnosticsHelper.Add(
                            list: diags,
                            code: DiagnosticCode.ElementInvalidNode,
                            message: $"Element at {childLogical} must be a mapping node.",
                            provenance: ProvenanceHelper.MakeProvenance(prov),
                            fallbackLocation: BuildLocation(sourcePath, childLogical)
                        );

                        elements.Add(new RawNamedInstanceElementDto(
                            AssignmentValue: null,
                            AssignmentNamedInstanceName: null,
                            ProvenanceStack: prov,
                            Diagnostics: diags.AsReadOnly()
                        ));
                    }
                }
            }

            // --- Exclusivity check ---
            if (assignments.Count > 0 && elements.Count > 0)
            {
                DiagnosticsHelper.Add(
                    list: diagnostics,
                    code: DiagnosticCode.NamedInstanceBothAssignmentsAndElementsSet,
                    message: $"Named instance '{name}' in {logicalPath} has both " +
                             "assignments and elements.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, logicalPath)
                );
            }

            // --- Aggregate child diagnostics ---
            diagnostics.AddRange(assignments.SelectMany(a => a.Diagnostics));
            diagnostics.AddRange(elements.SelectMany(e => e.Diagnostics));

            return new RawNamedInstanceDto(
                NamedInstanceName: name,
                QualifiedClassName: qualifiedClass,
                QualifiedClassBaseName: baseClass,
                QualifiedClassIsArray: classIsArray,
                QualifiedClassIsContainerNullable: classIsContainerNullable,
                QualifiedClassElementIsNullable: classIsElementNullable,
                QualifiedInterfaceName: exposeInterface,
                QualifiedInterfaceBaseName: baseIface,
                QualifiedInterfaceIsArray: ifaceIsArray,
                QualifiedInterfaceIsContainerNullable: ifaceIsContainerNullable,
                QualifiedInterfaceElementIsNullable: ifaceIsElementNullable,
                Assignments: assignments.AsReadOnly(),
                Elements: elements.AsReadOnly(),
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        private RawNamedInstanceAssignmentDto ParseAssignment(
            YamlMappingNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(
                node,
                typeof(RawNamedInstanceAssignmentDto),
                logicalPath,
                sourcePath).ToList();

            // --- ParameterName (SimpleName) ---
            var paramName = GetScalar(node, "parameterName");
            if (string.IsNullOrWhiteSpace(paramName))
            {
                DiagnosticsHelper.Add(
                    list: diagnostics,
                    code: DiagnosticCode.AssignmentMissingParameterName,
                    message: $"Missing 'parameterName' in {logicalPath}.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.parameterName")
                );
                paramName = "<missing.param>";
            }
            else if (!paramName.IsValidIdentifier() || paramName.Contains("."))
            {
                DiagnosticsHelper.Add(
                    list: diagnostics,
                    code: DiagnosticCode.InvalidIdentifier,
                    message: $"Assignment parameterName '{paramName}' must be a simple " +
                             "identifier (no namespace).",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.parameterName")
                );
            }

            // --- AssignedValue ---
            var value = GetScalar(node, "assignedValue");

            // --- AssignedNamedInstance (SimpleName or null) ---
            var assignedInstance = GetScalar(node, "assignedNamedInstance");
            if (string.Equals(
                assignedInstance,
                "null",
                StringComparison.OrdinalIgnoreCase))
            {
                assignedInstance = null;
            }
            else if (!string.IsNullOrWhiteSpace(assignedInstance) &&
                     (!assignedInstance.IsValidIdentifier() ||
                      assignedInstance.Contains(".")))
            {
                DiagnosticsHelper.Add(
                    list: diagnostics,
                    code: DiagnosticCode.InvalidIdentifier,
                    message: $"AssignedNamedInstance '{assignedInstance}' must be a simple " +
                             "identifier (no namespace).",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath,
                        $"{logicalPath}.assignedNamedInstance")
                );
            }

            return new RawNamedInstanceAssignmentDto(
                AssignmentParameterName: paramName,
                AssignmentValue: value,
                AssignmentNamedInstanceName: assignedInstance,
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        private RawNamedInstanceElementDto ParseElement(
            YamlMappingNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(
                node,
                typeof(RawNamedInstanceElementDto),
                logicalPath,
                sourcePath).ToList();

            var value = GetScalar(node, "assignedValue");
            if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                value = null;

            var assignedInstance = GetScalar(node, "assignedNamedInstance");
            if (string.Equals(assignedInstance, "null", StringComparison.OrdinalIgnoreCase))
                assignedInstance = null;

            // enforce exclusivity: one must be non-null, the other null
            if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(assignedInstance))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.ElementMissingValue,
                    $"Element at {logicalPath} must specify either assignedValue or assignedNamedInstance.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, logicalPath)
                );
            }
            else if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(assignedInstance))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.ElementBothValueAndInstance,
                    $"Element at {logicalPath} cannot specify both assignedValue and assignedNamedInstance.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, logicalPath)
                );
            }

            if (!string.IsNullOrWhiteSpace(assignedInstance) &&
                (!assignedInstance.IsValidIdentifier() || assignedInstance.Contains(".")))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.InvalidIdentifier,
                    $"AssignedNamedInstance '{assignedInstance}' must be a simple identifier.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.assignedNamedInstance")
                );
            }

            return new RawNamedInstanceElementDto(
                AssignmentValue: value,
                AssignmentNamedInstanceName: assignedInstance,
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }
    }
}

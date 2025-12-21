namespace DdiCodeGen.Dtos
{
    /// <summary>
    /// Instance-related transforms: named instances, assignments, elements.
    /// Each transform returns DTOs with per-node diagnostics; callers aggregate them.
    /// </summary>
    public sealed partial class Transformer
    {
        /// <summary>
        /// Transform a RawNamedInstanceDto into a NamedInstanceDto.
        /// Appends local diagnostics to rootDiagnostics.
        /// </summary>
        // Transform RawNamedInstanceDto into canonical NamedInstanceDto (keeps previous logic)
        internal NamedInstanceDto TransformNamedInstance(RawNamedInstanceDto raw, List<Diagnostic> rootDiagnostics)
        {
            var local = new List<Diagnostic>();

            // Provenance helpers for this node
            var nodeProv = ProvenanceHelper.MakeProvenance(raw?.ProvenanceStack);
            var nodeLocation = ProvenanceHelper.BuildLocationFromRaw(raw?.ProvenanceStack) ?? "<unknown>";

            // --- NamedInstanceName validation ---
            var name = raw?.NamedInstanceName ?? "<unnamed>";
            if (string.IsNullOrWhiteSpace(raw?.NamedInstanceName))
            {
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.NamedInstanceMissingName,
                    "NamedInstanceName is required.",
                    nodeProv,
                    nodeLocation
                );
            }
            else
            {
                if (!raw.NamedInstanceName.IsValidIdentifier())
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.InvalidIdentifier,
                        $"NamedInstanceName '{raw.NamedInstanceName}' is not a valid identifier.",
                        nodeProv,
                        nodeLocation
                    );
                }

                if (!raw.NamedInstanceName.IsPascalCase())
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.NamedInstanceMissingName,
                        $"NamedInstanceName '{raw.NamedInstanceName}' does not follow PascalCase convention.",
                        nodeProv,
                        nodeLocation
                    );
                }
            }

            // --- QualifiedClassName validation ---
            if (string.IsNullOrWhiteSpace(raw?.QualifiedClassName))
            {
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.NamedInstanceMissingQualifiedClass,
                    "QualifiedClassName is required for named instance.",
                    nodeProv,
                    nodeLocation
                );
            }
            else
            {
                if (string.IsNullOrWhiteSpace(raw.QualifiedClassBaseName))
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.NamedInstanceMissingQualifiedClass,
                        $"QualifiedClassName '{raw.QualifiedClassName}' is empty after parsing.",
                        nodeProv,
                        nodeLocation
                    );
                }
                else if (!raw.QualifiedClassBaseName.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.NamedInstanceMissingQualifiedClass,
                        $"QualifiedClassName '{raw.QualifiedClassName}' has invalid base name '{raw.QualifiedClassBaseName}'.",
                        nodeProv,
                        nodeLocation
                    );
                }
            }

            // --- QualifiedInterfaceName validation ---
            if (!string.IsNullOrWhiteSpace(raw?.QualifiedInterfaceName))
            {
                if (string.IsNullOrWhiteSpace(raw.QualifiedInterfaceBaseName))
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.InterfaceMissingQualifiedName,
                        $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' is empty after parsing.",
                        nodeProv,
                        nodeLocation
                    );
                }
                else if (!raw.QualifiedInterfaceBaseName.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.InterfaceMissingQualifiedName,
                        $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' has invalid base name '{raw.QualifiedInterfaceBaseName}'.",
                        nodeProv,
                        nodeLocation
                    );
                }
            }

            // --- Transform assignments and elements ---
            var assignments = new List<NamedInstanceAssignmentDto>();
            var elements = new List<NamedInstanceElementDto>();

            if (raw?.Assignments != null)
            {
                foreach (var rawAssign in raw.Assignments)
                {
                    var assignDto = TransformAssignment(rawAssign, out var assignDiags);
                    assignments.Add(assignDto);
                    if (assignDiags != null && assignDiags.Count > 0)
                        local.AddRange(assignDiags);
                }
            }

            if (raw?.Elements != null)
            {
                foreach (var rawElem in raw.Elements)
                {
                    var elemDto = TransformElement(rawElem, out var elemDiags);
                    elements.Add(elemDto);
                    if (elemDiags != null && elemDiags.Count > 0)
                        local.AddRange(elemDiags);
                }
            }

            // --- Include any diagnostics produced during raw parsing ---
            if (raw?.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            // --- Propagate to root diagnostics ---
            rootDiagnostics.AddRange(local);

            if (raw is null) throw new ArgumentNullException(nameof(raw));

            // --- Construct canonical NamedInstanceDto ---
            return new NamedInstanceDto(
                namedInstanceName: name,
                qualifiedClassName: raw?.QualifiedClassBaseName ?? "<missing>",
                qualifiedInterfaceName: raw?.QualifiedInterfaceBaseName,   // NEW
                isArray: raw!.QualifiedClassIsArray,
                isNullable: raw.QualifiedClassIsContainerNullable,
                elementIsNullable: raw.QualifiedClassElementIsNullable,
                interfaceIsArray: raw.QualifiedInterfaceIsArray,          // NEW
                interfaceIsNullable: raw.QualifiedInterfaceIsContainerNullable, // NEW
                interfaceElementIsNullable: raw.QualifiedInterfaceElementIsNullable, // NEW
                assignments: assignments.AsReadOnly(),
                elements: elements.AsReadOnly(),
                provenanceStack: ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                diagnostics: local.AsReadOnly()
            );
        }

        /// <summary>
        /// Transform a RawNamedInstanceAssignmentDto into a NamedInstanceAssignmentDto and return diagnostics.
        /// </summary>
        private NamedInstanceAssignmentDto TransformAssignment(RawNamedInstanceAssignmentDto raw, out IReadOnlyList<Diagnostic> outDiagnostics)
        {
            var local = new List<Diagnostic>();

            if (raw is null)
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.AssignmentInvalidNode, "Assignment node is null.", (ProvenanceStack?)null, "<unknown>");
                outDiagnostics = local.ToList().AsReadOnly();
                var provNull = ProvenanceHelper.MakeProvenance((ProvenanceStack?)null, "<unknown>");
                return new NamedInstanceAssignmentDto(
                    assignmentParameterName: "<missing>",
                    assignmentValue: null,
                    assignmentNamedInstanceName: null,
                    provenanceStack: provNull,
                    diagnostics: outDiagnostics
                );
            }

            var provRaw = ProvenanceHelper.MakeProvenance(raw.ProvenanceStack);
            var fallbackLocation = ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack) ?? "<unknown>";
            var prov = ProvenanceHelper.MakeProvenance(provRaw, fallbackLocation);

            try
            {
                var paramName = string.IsNullOrWhiteSpace(raw.AssignmentParameterName) ? "<missing>" : raw.AssignmentParameterName.Trim();

                if (string.IsNullOrWhiteSpace(raw.AssignmentParameterName))
                    DiagnosticsHelper.Add(local, DiagnosticCode.AssignmentMissingParameterName, "Assignment parameter name is required.", prov, fallbackLocation);
                else if (!paramName.IsValidIdentifier())
                    DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"Assignment parameter name '{paramName}' is not a valid identifier.", prov, fallbackLocation);

                var value = string.IsNullOrWhiteSpace(raw.AssignmentValue) ? null : raw.AssignmentValue.Trim();
                var target = string.IsNullOrWhiteSpace(raw.AssignmentNamedInstanceName) ? null : raw.AssignmentNamedInstanceName.Trim();

                if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(target))
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.AssignmentMissingValueOrInstance, "Assignment must specify either a value or a named instance, not both.", prov, fallbackLocation);
                    // Policy: prefer explicit value; ignore target
                    target = null;
                }
                else if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(target))
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.AssignmentMissingValueOrInstance, "Assignment must specify either a value or a named instance.", prov, fallbackLocation);
                }

                if (!string.IsNullOrWhiteSpace(target))
                {
                    if (!target.IsValidIdentifier())
                        DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"Assigned named instance '{target}' is not a valid identifier.", prov, fallbackLocation);
                    if (!target.IsPascalCase())
                        DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"Assigned named instance '{target}' does not follow PascalCase convention.", prov, fallbackLocation);
                }

                if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                    local.AddRange(raw.Diagnostics);

                outDiagnostics = local.ToList().AsReadOnly();

                var dto = new NamedInstanceAssignmentDto(
                    assignmentParameterName: paramName,
                    assignmentValue: value,
                    assignmentNamedInstanceName: target,
                    provenanceStack: prov,
                    diagnostics: outDiagnostics
                );

                return dto;
            }
            catch (Exception ex)
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.DtoValidationException, $"Exception while transforming assignment: {ex.Message}", prov, fallbackLocation);
                outDiagnostics = local.ToList().AsReadOnly();

                return new NamedInstanceAssignmentDto(
                    assignmentParameterName: raw?.AssignmentParameterName?.Trim() ?? "<missing>",
                    assignmentValue: raw?.AssignmentValue,
                    assignmentNamedInstanceName: raw?.AssignmentNamedInstanceName?.Trim(),
                    provenanceStack: prov,
                    diagnostics: outDiagnostics
                );
            }
        }

        /// <summary>
        /// Transform a RawNamedInstanceElementDto into a NamedInstanceElementDto and return diagnostics.
        /// </summary>
        private NamedInstanceElementDto TransformElement(RawNamedInstanceElementDto raw, out IReadOnlyList<Diagnostic> outDiagnostics)
        {
            var local = new List<Diagnostic>();

            if (raw is null)
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.ElementInvalidNode, "Element node is null.", (ProvenanceStack?)null, "<unknown>");
                outDiagnostics = local.ToList().AsReadOnly();
                var provNull = ProvenanceHelper.MakeProvenance((ProvenanceStack?)null, "<unknown>");
                return new NamedInstanceElementDto(
                    assignmentValue: null,
                    assignmentNamedInstanceName: null,
                    provenanceStack: provNull,
                    diagnostics: outDiagnostics
                );
            }

            var provRaw = ProvenanceHelper.MakeProvenance(raw.ProvenanceStack);
            var fallbackLocation = ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack) ?? "<unknown>";
            var prov = ProvenanceHelper.MakeProvenance(provRaw, fallbackLocation);

            try
            {
                var value = string.IsNullOrWhiteSpace(raw.AssignmentValue) ? null : raw.AssignmentValue.Trim();
                var target = string.IsNullOrWhiteSpace(raw.AssignmentNamedInstanceName) ? null : raw.AssignmentNamedInstanceName.Trim();

                if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(target))
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.ElementMissingValueOrInstance, "Element must specify either a value or a named instance, not both.", prov, fallbackLocation);
                    // Policy: prefer explicit value; ignore target
                    target = null;
                }
                else if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(target))
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.ElementMissingValueOrInstance, "Element must specify either a value or a named instance.", prov, fallbackLocation);
                }

                if (!string.IsNullOrWhiteSpace(target))
                {
                    if (!target.IsValidIdentifier())
                        DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"Element namedInstanceName '{target}' is not a valid identifier.", prov, fallbackLocation);
                    if (!target.IsPascalCase())
                        DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"Element namedInstanceName '{target}' does not follow PascalCase convention.", prov, fallbackLocation);
                }

                if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                    local.AddRange(raw.Diagnostics);

                outDiagnostics = local.ToList().AsReadOnly();

                var dto = new NamedInstanceElementDto(
                    assignmentValue: value,
                    assignmentNamedInstanceName: target,
                    provenanceStack: prov,
                    diagnostics: outDiagnostics
                );

                return dto;
            }
            catch (Exception ex)
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.DtoValidationException, $"Exception while transforming element: {ex.Message}", prov, fallbackLocation);
                outDiagnostics = local.ToList().AsReadOnly();

                return new NamedInstanceElementDto(
                    assignmentValue: raw?.AssignmentValue,
                    assignmentNamedInstanceName: raw?.AssignmentNamedInstanceName,
                    provenanceStack: prov,
                    diagnostics: outDiagnostics
                );
            }
        }
    }
}

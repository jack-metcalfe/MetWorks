using System;
using System.Collections.Generic;
using System.Linq;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Dtos.Raw;
using DdiCodeGen.Validation;

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

            // --- NamedInstanceName validation ---
            var name = raw.NamedInstanceName ?? "<unnamed>";
            if (string.IsNullOrWhiteSpace(raw.NamedInstanceName))
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.NamedInstanceMissingName,
                    "NamedInstanceName is required.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }
            else
            {
                if (!raw.NamedInstanceName.IsValidIdentifier())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.InvalidIdentifier,
                        $"NamedInstanceName '{raw.NamedInstanceName}' is not a valid identifier.",
                        
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }

                if (!raw.NamedInstanceName.IsPascalCase())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.NamedInstanceMissingName,
                        $"NamedInstanceName '{raw.NamedInstanceName}' does not follow PascalCase convention.",
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }
            }

            // --- QualifiedClassName validation ---
            if (string.IsNullOrWhiteSpace(raw.QualifiedClassName))
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.NamedInstanceMissingQualifiedClass,
                    "QualifiedClassName is required for named instance.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(raw.QualifiedClassBaseName))
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.NamedInstanceMissingQualifiedClass,
                        $"QualifiedClassName '{raw.QualifiedClassName}' is empty after parsing.",
                        
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }
                else if (!raw.QualifiedClassBaseName.IsQualifiedName())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.NamedInstanceMissingQualifiedClass,
                        $"QualifiedClassName '{raw.QualifiedClassName}' has invalid base name '{raw.QualifiedClassBaseName}'.",
                        
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }
            }

            // --- QualifiedInterfaceName validation ---
            if (!string.IsNullOrWhiteSpace(raw.QualifiedInterfaceName))
            {
                if (string.IsNullOrWhiteSpace(raw.QualifiedInterfaceBaseName))
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.InterfaceMissingQualifiedName,
                        $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' is empty after parsing.",
                        
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }
                else if (!raw.QualifiedInterfaceBaseName.IsQualifiedName())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.InterfaceMissingQualifiedName,
                        $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' has invalid base name '{raw.QualifiedInterfaceBaseName}'.",
                        
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }
            }

            // --- Transform assignments and elements ---
            var assignments = new List<NamedInstanceAssignmentDto>();
            var elements = new List<NamedInstanceElementDto>();

            if (raw.Assignments != null)
            {
                foreach (var rawAssign in raw.Assignments)
                {
                    var assignDto = TransformAssignment(rawAssign, out var assignDiags);
                    assignments.Add(assignDto);
                    if (assignDiags != null && assignDiags.Count > 0)
                        local.AddRange(assignDiags);
                }
            }

            if (raw.Elements != null)
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
            if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            // --- Propagate to root diagnostics ---
            rootDiagnostics.AddRange(local);

            // --- Construct canonical NamedInstanceDto ---
            return new NamedInstanceDto(
                namedInstanceName: name,
                qualifiedClassName: raw.QualifiedClassBaseName ?? "<missing>",
                qualifiedInterfaceName: raw.QualifiedInterfaceBaseName,   // NEW
                isArray: raw.QualifiedClassIsArray,
                isNullable: raw.QualifiedClassIsContainerNullable,
                elementIsNullable: raw.QualifiedClassElementIsNullable,
                interfaceIsArray: raw.QualifiedInterfaceIsArray,          // NEW
                interfaceIsNullable: raw.QualifiedInterfaceIsContainerNullable, // NEW
                interfaceElementIsNullable: raw.QualifiedInterfaceElementIsNullable, // NEW
                assignments: assignments.AsReadOnly(),
                elements: elements.AsReadOnly(),
                provenanceStack: ProvenanceHelper.TransformProvenanceFromRaw(raw.ProvenanceStack),
                diagnostics: local.AsReadOnly()
            );
        }

        /// <summary>
        /// Transform a RawNamedInstanceAssignmentDto into a NamedInstanceAssignmentDto and return diagnostics.
        /// </summary>
        private NamedInstanceAssignmentDto TransformAssignment(RawNamedInstanceAssignmentDto raw,
            out IReadOnlyList<Diagnostic> outDiagnostics)
        {
            var local = new List<Diagnostic>();

            var paramName = raw.ParameterName ?? "<missing>";
            if (string.IsNullOrWhiteSpace(raw.ParameterName))
                local.Add(new Diagnostic(DiagnosticCode.AssignmentMissingParameterName,
                "Assignment parameter name is required.", 
                ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
            else if (!raw.ParameterName.IsValidIdentifier())
                local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier,
                $"Assignment parameter name '{raw.ParameterName}' is not a valid identifier.",
                 ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));

            var value = raw.AssignedValue;
            var target = raw.AssignedNamedInstance;

            if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(target))
                local.Add(new Diagnostic(DiagnosticCode.AssignmentMissingValueOrInstance,
                "Assignment must specify either a value or a named instance, not both.",
                 ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
            else if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(target))
                local.Add(new Diagnostic(DiagnosticCode.AssignmentMissingValueOrInstance,
                "Assignment must specify either a value or a named instance.",
                 ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
            else if (!string.IsNullOrWhiteSpace(target))
            {
                if (!target.IsValidIdentifier())
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier,
                    $"Assigned named instance '{target}' is not a valid identifier.",
                     ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
                if (!target.IsPascalCase())
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier,
                    $"Assigned named instance '{target}' does not follow PascalCase convention.",
                    ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
            }

            if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            outDiagnostics = local.ToList().AsReadOnly();

            return new NamedInstanceAssignmentDto(
                assignmentParameterName: paramName,
                assignmentValue: value,
                namedInstanceName: target,
                provenanceStack: ProvenanceHelper.TransformProvenance(raw.ProvenanceStack),
                diagnostics: outDiagnostics
            );
        }

        /// <summary>
        /// Transform a RawNamedInstanceElementDto into a NamedInstanceElementDto and return diagnostics.
        /// </summary>
        private NamedInstanceElementDto TransformElement(RawNamedInstanceElementDto raw, 
            out IReadOnlyList<Diagnostic> outDiagnostics)
        {
            var local = new List<Diagnostic>();

            var value = raw.Value;
            var target = raw.AssignedNamedInstance;

            if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(target))
                local.Add(new Diagnostic(DiagnosticCode.ElementMissingValueOrInstance,
                "Element must specify either a value or a named instance, not both.",
                 ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
            else if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(target))
                local.Add(new Diagnostic(DiagnosticCode.ElementMissingValueOrInstance,
                "Element must specify either a value or a named instance.",
                 ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
            else if (!string.IsNullOrWhiteSpace(target))
            {
                if (!target.IsValidIdentifier())
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier,
                    $"Element namedInstanceName '{target}' is not a valid identifier.",
                     ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
                if (!target.IsPascalCase())
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier,
                    $"Element namedInstanceName '{target}' does not follow PascalCase convention.",
                    ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack)));
            }

            if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            outDiagnostics = local.ToList().AsReadOnly();

            return new NamedInstanceElementDto(
                value: value,
                namedInstanceName: target,
                provenanceStack: ProvenanceHelper.TransformProvenance(raw.ProvenanceStack),
                diagnostics: outDiagnostics
            );
        }
    }
}

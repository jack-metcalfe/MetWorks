using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.RepresentationModel;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Validation;

namespace DdiCodeGen.Dtos.Internal
{
    internal sealed partial class Loader
    {
        // --- YamlRawModelLoader.Instances.cs (excerpt with updated ParseNamedInstance) ---

        private RawNamedInstanceDto ParseNamedInstance(YamlMappingNode node, string sourcePath, string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(node, typeof(RawNamedInstanceDto), logicalPath, sourcePath).ToList();

            // --- NamedInstanceName ---
            var name = GetScalar(node, "namedInstanceName");
            if (string.IsNullOrWhiteSpace(name))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamedInstanceMissingName,
                    $"Missing 'namedInstanceName' in {logicalPath}.",
                    
                    BuildLocation(sourcePath, $"{logicalPath}.namedInstanceName")));
                name = "<missing.instance>";
            }
            else if (!name.IsValidIdentifier())
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.InvalidIdentifier,
                    $"NamedInstanceName '{name}' is not a valid identifier.",
                    
                    BuildLocation(sourcePath, $"{logicalPath}.namedInstanceName")));
            }

            // --- QualifiedClassName ---
            var qualifiedClass = GetScalar(node, "qualifiedClassName");
            string? baseClass = null;
            bool classIsArray = false, classIsContainerNullable = false, classIsElementNullable = false;

            if (string.IsNullOrWhiteSpace(qualifiedClass))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamedInstanceMissingQualifiedClass,
                    $"Missing 'qualifiedClassName' in {logicalPath}.",
                    
                    BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName")));
                qualifiedClass = "<missing.qualifiedClass>";
            }
            else
            {
                if (!qualifiedClass.TryParseTypeRef(out baseClass, out classIsArray, out classIsContainerNullable, out classIsElementNullable))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{qualifiedClass}' in {logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', 'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside arrays (e.g., 'Ns.Type?[]') are not supported.",
                        
                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName")));

                    baseClass = null;
                    classIsArray = false;
                    classIsContainerNullable = false;
                    classIsElementNullable = false;
                }
                else if (string.IsNullOrWhiteSpace(baseClass) || !baseClass.IsQualifiedName())
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.NamedInstanceMissingQualifiedClass,
                        $"QualifiedClassName '{qualifiedClass}' is not a valid qualified name after parsing.",
                        
                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName")));
                }
            }

            // --- QualifiedInterfaceName ---
            var exposeInterface = GetScalar(node, "qualifiedInterfaceName");
            string? baseIface = null;
            bool ifaceIsArray = false, ifaceIsContainerNullable = false, ifaceIsElementNullable = false;

            if (!string.IsNullOrWhiteSpace(exposeInterface))
            {
                if (!exposeInterface.TryParseTypeRef(out baseIface, out ifaceIsArray, out ifaceIsContainerNullable, out ifaceIsElementNullable))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{exposeInterface}' in {logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', 'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside arrays (e.g., 'Ns.Type?[]') are not supported.",
                        
                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")));

                    baseIface = null;
                    ifaceIsArray = false;
                    ifaceIsContainerNullable = false;
                    ifaceIsElementNullable = false;
                }
                else if (string.IsNullOrWhiteSpace(baseIface) || !baseIface.IsQualifiedName())
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.InterfaceMissingQualifiedName,
                        $"QualifiedInterfaceName '{exposeInterface}' is not a valid qualified name after parsing.",
                        
                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")));
                }
            }

            // --- Assignments ---
            var assignmentsSeq = GetChildSequence(node, "assignments");
            var assignments = new List<RawNamedInstanceAssignmentDto>();
            if (assignmentsSeq != null)
            {
                int idx = 0;
                foreach (var child in assignmentsSeq.Children.OfType<YamlMappingNode>())
                {
                    idx++;
                    assignments.Add(ParseAssignment(child, sourcePath, $"{logicalPath}.assignments[{idx}]"));
                }
            }

            // --- Elements ---
            var elementsSeq = GetChildSequence(node, "elements");
            var elements = new List<RawNamedInstanceElementDto>();
            if (elementsSeq != null)
            {
                int idx = 0;
                foreach (var child in elementsSeq.Children.OfType<YamlMappingNode>())
                {
                    idx++;
                    elements.Add(ParseElement(child, sourcePath, $"{logicalPath}.elements[{idx}]"));
                }
            }

            // --- Exclusivity check ---
            if (assignments.Count > 0 && elements.Count > 0)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamedInstanceBothAssignmentsAndElementsSet,
                    $"Named instance '{name}' in {logicalPath} has both assignments and elements.",
                    
                    BuildLocation(sourcePath, logicalPath)));
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

        private RawNamedInstanceAssignmentDto ParseAssignment(YamlMappingNode node, string sourcePath, string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(node, typeof(RawNamedInstanceAssignmentDto), logicalPath, sourcePath).ToList();

            var paramName = GetScalar(node, "parameterName") ?? GetScalar(node, "assignmentParameterName");
            if (string.IsNullOrWhiteSpace(paramName))
            {
                diagnostics.Add(new Diagnostic(DiagnosticCode.AssignmentMissingParameterName, $"Missing 'parameterName' in {logicalPath}.",  BuildLocation(sourcePath, $"{logicalPath}.parameterName")));
                paramName = "<missing.param>";
            }
            else if (!paramName.IsValidIdentifier())
            {
                diagnostics.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"Assignment parameter name '{paramName}' is not a valid identifier.",  BuildLocation(sourcePath, $"{logicalPath}.parameterName")));
            }

            var assignedValue = GetScalar(node, "assignedValue") ?? GetScalar(node, "value");
            var assignedNamedInstance = GetScalar(node, "assignedNamedInstance");

            if (!string.IsNullOrWhiteSpace(assignedValue) && !string.IsNullOrWhiteSpace(assignedNamedInstance))
            {
                diagnostics.Add(new Diagnostic(DiagnosticCode.AssignmentMissingValueOrInstance, $"Assignment at {logicalPath} specifies both value and namedInstanceName.",  BuildLocation(sourcePath, logicalPath)));
            }
            else if (string.IsNullOrWhiteSpace(assignedValue) && string.IsNullOrWhiteSpace(assignedNamedInstance))
            {
                diagnostics.Add(new Diagnostic(DiagnosticCode.AssignmentMissingValueOrInstance, $"Assignment at {logicalPath} must specify either value or namedInstanceName.",  BuildLocation(sourcePath, logicalPath)));
            }
            else if (!string.IsNullOrWhiteSpace(assignedNamedInstance))
            {
                if (!assignedNamedInstance.IsValidIdentifier())
                    diagnostics.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"Assigned named instance '{assignedNamedInstance}' is not a valid identifier.",  BuildLocation(sourcePath, $"{logicalPath}.namedInstanceName")));
            }

            return new RawNamedInstanceAssignmentDto(
                ParameterName: paramName,
                AssignedValue: assignedValue,
                AssignedNamedInstance: assignedNamedInstance,
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.ToList().AsReadOnly()
            );
        }

        private RawNamedInstanceElementDto ParseElement(YamlMappingNode node, string sourcePath, string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(node, typeof(RawNamedInstanceElementDto), logicalPath, sourcePath).ToList();

            var value = GetScalar(node, "assignedValue");
            var namedInstanceName = GetScalar(node, "assignedNamedInstance");

            if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(namedInstanceName))
            {
                diagnostics.Add(new Diagnostic(DiagnosticCode.ElementMissingValueOrInstance, $"Element at {logicalPath} specifies both value and namedInstanceName.",  BuildLocation(sourcePath, logicalPath)));
            }
            else if (string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(namedInstanceName))
            {
                diagnostics.Add(new Diagnostic(DiagnosticCode.ElementMissingValueOrInstance, $"Element at {logicalPath} must specify either value or namedInstanceName.",  BuildLocation(sourcePath, logicalPath)));
            }
            else if (!string.IsNullOrWhiteSpace(namedInstanceName))
            {
                if (!namedInstanceName.IsValidIdentifier())
                    diagnostics.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"Element namedInstanceName '{namedInstanceName}' is not a valid identifier.",  BuildLocation(sourcePath, $"{logicalPath}.namedInstanceName")));
            }

            return new RawNamedInstanceElementDto(
                Value: value,
                AssignedNamedInstance: namedInstanceName,
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.ToList().AsReadOnly()
            );
        }
    }
}

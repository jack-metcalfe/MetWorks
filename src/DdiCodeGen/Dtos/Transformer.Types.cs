using System;
using System.Collections.Generic;
using System.Linq;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Dtos.Raw;
using DdiCodeGen.Validation;

namespace DdiCodeGen.Dtos
{
    /// <summary>
    /// Type-related transforms: namespaces, interfaces, classes, parameters.
    /// Each transform produces per-node diagnostics which are aggregated by the caller.
    /// </summary>
    public sealed partial class Transformer
    {
        // Transform a RawNamespaceDto into a NamespaceDto (canonical)
        private NamespaceDto TransformNamespace(RawNamespaceDto raw, List<Diagnostic> rootDiagnostics)
        {
            var local = new List<Diagnostic>();

            var nsName = raw.NamespaceName ?? "<missing>";
            if (string.IsNullOrWhiteSpace(raw.NamespaceName))
            {
                local.Add(new Diagnostic(DiagnosticCode.NamespaceMissingName, "NamespaceName is required.",
                 ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }
            else if (!raw.NamespaceName.IsValidNamespace())
            {
                local.Add(new Diagnostic(DiagnosticCode.NamespaceInvalidSegment,
                $"Namespace '{raw.NamespaceName}' is not a valid namespace.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }

            // Transform interfaces
            var interfaces = new List<InterfaceDto>();
            foreach (var rawIface in raw.Interfaces ?? Array.Empty<RawInterfaceDto>())
            {
                var ifaceDto = TransformInterface(rawIface, nsName, local);
                interfaces.Add(ifaceDto);
            }

            // Transform classes
            var classes = new List<ClassDto>();
            foreach (var rawClass in raw.Classes ?? Array.Empty<RawClassDto>())
            {
                try
                {
                    var classDto = TransformClass(rawClass, nsName, local);
                    classes.Add(classDto);
                }
                catch (ArgumentException aex)
                {
                    var loc = ProvenanceHelper.BuildLocationFromRaw(rawClass.ProvenanceStack) ?? $"{nsName}.<class>";
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"Failed to transform class '{rawClass.ClassName ?? "<missing>"}': {aex.Message}",  loc));
                }
                catch (Exception ex)
                {
                    var loc = ProvenanceHelper.BuildLocationFromRaw(rawClass.ProvenanceStack) ?? $"{nsName}.<class>";
                    local.Add(new Diagnostic(DiagnosticCode.DtoValidationException, $"Unexpected error while transforming class '{rawClass.ClassName ?? "<missing>"}': {ex.Message}",  loc));
                }
            }

            if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            rootDiagnostics.AddRange(local);

            return new NamespaceDto(
                namespaceName: nsName,
                interfaces: interfaces.ToList().AsReadOnly(),
                classes: classes.ToList().AsReadOnly(),
                provenanceStack: ProvenanceHelper.TransformProvenanceFromRaw(raw.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );
        }

        // Transform a RawInterfaceDto into a canonical InterfaceDto
        private InterfaceDto TransformInterface(RawInterfaceDto raw, string parentNamespace, List<Diagnostic> localDiagnostics)
        {
            var local = new List<Diagnostic>();

            if (string.IsNullOrWhiteSpace(raw.InterfaceName))
            {
                local.Add(new Diagnostic(DiagnosticCode.InterfaceMissingName, "InterfaceName is required.",
                 ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }
            else
            {
                if (!raw.InterfaceName.IsValidIdentifier())
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier,
                    $"InterfaceName '{raw.InterfaceName}' is not a valid identifier.",
                     ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                if (!raw.InterfaceName.IsInterfaceName())
                    local.Add(
                        new Diagnostic(
                            DiagnosticCode.InterfaceMissingName,
                            $"InterfaceName '{raw.InterfaceName}' does not follow interface naming convention.",
                            ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }

            localDiagnostics.AddRange(local);

            var shortName = raw.InterfaceName ?? "<missing>";
            var qualified = string.IsNullOrWhiteSpace(raw.InterfaceName) ? "<missing>" : $"{parentNamespace}.{shortName}";

            return new InterfaceDto(
                interfaceName: shortName,
                qualifiedInterfaceName: qualified,
                provenanceStack: ProvenanceHelper.TransformProvenanceFromRaw(raw.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );
        }

        // Transform a RawParameterDto into a canonical ParameterDto
        private ParameterDto TransformParameter(RawParameterDto raw, out IReadOnlyList<Diagnostic> outDiagnostics)
        {
            var local = new List<Diagnostic>();
            var location = ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack);

            // --- ParameterName validation ---
            if (string.IsNullOrWhiteSpace(raw.ParameterName))
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.ParameterMissingName,
                    "ParameterName is required.",
                    
                    location));
            }
            else if (!raw.ParameterName.IsValidIdentifier())
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.InvalidIdentifier,
                    $"ParameterName '{raw.ParameterName}' is not a valid identifier.",
                    
                    location));
            }

            var hasClassToken = !string.IsNullOrWhiteSpace(raw.QualifiedClassName);
            var hasInterfaceToken = !string.IsNullOrWhiteSpace(raw.QualifiedInterfaceName);

            if (hasClassToken && hasInterfaceToken)
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.ParameterBothClassAndInterface,
                    "Parameter cannot specify both QualifiedClassName and QualifiedInterfaceName.",
                    
                    location));
            }
            else if (!hasClassToken && !hasInterfaceToken)
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.ParameterMissingClassOrInterface,
                    "Parameter must specify either QualifiedClassName or QualifiedInterfaceName.",
                    
                    location));
            }

            // --- Base name validation (use parsed fields from Raw DTO) ---
            if (hasClassToken)
            {
                if (string.IsNullOrWhiteSpace(raw.QualifiedClassBaseName))
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.ParameterMissingQualifiedClass,
                        $"QualifiedClassName '{raw.QualifiedClassName}' is empty after parsing.",
                        
                        location));
                }
                else if (!raw.QualifiedClassBaseName.IsQualifiedName())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.ParameterMissingQualifiedClass,
                        $"QualifiedClassName '{raw.QualifiedClassName}' has invalid base name '{raw.QualifiedClassBaseName}'.",
                        
                        location));
                }
            }

            if (hasInterfaceToken)
            {
                if (string.IsNullOrWhiteSpace(raw.QualifiedInterfaceBaseName))
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.ParameterMissingQualifiedInterface,
                        $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' is empty after parsing.",
                        
                        location));
                }
                else if (!raw.QualifiedInterfaceBaseName.IsQualifiedName())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.ParameterMissingQualifiedInterface,
                        $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' has invalid base name '{raw.QualifiedInterfaceBaseName}'.",
                        
                        location));
                }
            }

            // --- Propagate raw diagnostics ---
            if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            outDiagnostics = local.ToList().AsReadOnly();

            // --- Construct canonical ParameterDto ---
            return new ParameterDto(
                parameterName: raw.ParameterName ?? "<missing>",
                qualifiedClassName: raw.QualifiedClassBaseName,
                qualifiedInterfaceName: raw.QualifiedInterfaceBaseName,
                isArray: raw.QualifiedClassIsArray || raw.QualifiedInterfaceIsArray,
                isNullable: raw.QualifiedClassIsContainerNullable || raw.QualifiedInterfaceIsContainerNullable,
                elementIsNullable: raw.QualifiedClassElementIsNullable || raw.QualifiedInterfaceElementIsNullable,
                isValid: !outDiagnostics.Any(d => DiagnosticCodeInfo.GetSeverity(d.DiagnosticCode) == DiagnosticSeverity.Error),
                diagnostics: outDiagnostics,
                provenanceStack: ProvenanceHelper.TransformProvenanceFromRaw(raw.ProvenanceStack)
            );
        }
        // Transform a RawClassDto into a canonical ClassDto
        private ClassDto TransformClass(RawClassDto raw, string parentNamespace, List<Diagnostic> localDiagnostics)
        {
            var local = new List<Diagnostic>();

            // 1) Short name checks
            if (string.IsNullOrWhiteSpace(raw.ClassName))
            {
                local.Add(new Diagnostic(DiagnosticCode.ClassMissingName, "ClassName is required.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }
            else
            {
                if (raw.ClassName.Contains('.') || raw.ClassName.IsQualifiedName())
                {
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"ClassName '{raw.ClassName}' must be an unqualified short name; found qualified token. Define the class under the correct namespace instead.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }

                if (!raw.ClassName.IsValidIdentifier())
                {
                    local.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"ClassName '{raw.ClassName}' is not a valid identifier.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }

                if (!raw.ClassName.IsPascalCase())
                {
                    local.Add(new Diagnostic(DiagnosticCode.ClassMissingName, $"ClassName '{raw.ClassName}' does not follow PascalCase convention.", ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }
            }

            // 2) Parameters
            var parameterDtos = new List<ParameterDto>();

            // If raw.InitializerParameters is null => the YAML key was missing -> emit error.
            // If it's non-null (even if empty), parse and accept empty list.
            if (raw.InitializerParameters is null)
            {
                local.Add(new Diagnostic(DiagnosticCode.ClassMissingParameters, $"Missing required 'initializerParameters' for class '{raw.ClassName ?? "<missing>"}'.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }
            else
            {
                foreach (var rawParam in raw.InitializerParameters)
                {
                    var paramDto = TransformParameter(rawParam, out var paramDiagnostics);
                    parameterDtos.Add(paramDto);
                    local.AddRange(paramDiagnostics);
                }
                // NOTE: do not treat an empty parameterDtos list as an error here — explicit [] is allowed.
            }

            if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            // 3) Compose canonical qualified class name (namespace + short name)
            var shortName = raw.ClassName ?? "<missing>";
            var qualifiedClassName = $"{parentNamespace}.{shortName}";

            if (!qualifiedClassName.IsQualifiedName())
            {
                local.Add(new Diagnostic(DiagnosticCode.NamespaceInvalidSegment, $"Composed qualified class name '{qualifiedClassName}' is not a valid qualified name.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
            }

            // 4) Determine effective return type (qualified)
            string effectiveReturnQualified = qualifiedClassName;

            if (!string.IsNullOrWhiteSpace(raw.QualifiedInterfaceName))
            {
                if (!raw.QualifiedInterfaceName.IsQualifiedName())
                {
                    local.Add(new Diagnostic(DiagnosticCode.InterfaceMissingQualifiedName, $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' is not a valid qualified name.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                }
                else
                {
                    var shortIface = raw.QualifiedInterfaceName.ExtractShortName();
                    if (!shortIface.IsInterfaceName())
                    {
                        local.Add(new Diagnostic(DiagnosticCode.InterfaceMissingQualifiedName, $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' does not resolve to a valid interface name.",  ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)));
                    }
                    else
                    {
                        effectiveReturnQualified = raw.QualifiedInterfaceName.ExtractBaseQualifiedName();
                    }
                }
            }

            // 5) Generate invoker key from canonical qualified class name
            var invokerKey = CanonicalHelpers.GenerateInvokerKeyFromQualified(qualifiedClassName);

            // 6) Aggregate diagnostics and construct canonical DTO
            localDiagnostics.AddRange(local);

            var dto = new ClassDto(
                className: shortName,
                shortName: shortName,
                invokerKey: invokerKey,
                qualifiedClassName: qualifiedClassName,
                qualifiedInterfaceName: raw.QualifiedInterfaceName,
                returnTypeQualifiedName: effectiveReturnQualified,
                initializerParameters: parameterDtos.ToList().AsReadOnly(),
                provenanceStack: ProvenanceHelper.TransformProvenanceFromRaw(raw.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );

            return dto;
        }
    }
}

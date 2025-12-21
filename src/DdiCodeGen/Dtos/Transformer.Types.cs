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
            var nsProv = ProvenanceHelper.MakeProvenance(raw?.ProvenanceStack);
            var nsLocation = ProvenanceHelper.BuildLocationFromRaw(raw?.ProvenanceStack) ?? "<unknown>";

            if (string.IsNullOrWhiteSpace(raw?.NamespaceName))
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.NamespaceMissingName, "NamespaceName is required.", nsProv, nsLocation);
            }
            else if (!raw.NamespaceName.IsValidNamespace())
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.NamespaceInvalidSegment, $"Namespace '{raw.NamespaceName}' is not a valid namespace.", nsProv, nsLocation);
            }

            // Transform interfaces
            var interfaces = new List<InterfaceDto>();
            foreach (var rawIface in raw?.Interfaces ?? Array.Empty<RawInterfaceDto>())
            {
                var ifaceDto = TransformInterface(rawIface, nsName, local);
                interfaces.Add(ifaceDto);
            }

            // Transform classes
            var classes = new List<ClassDto>();
            foreach (var rawClass in raw?.Classes ?? Array.Empty<RawClassDto>())
            {
                try
                {
                    var classDto = TransformClass(rawClass, nsName, local);
                    classes.Add(classDto);
                }
                catch (ArgumentException aex)
                {
                    var locProv = ProvenanceHelper.MakeProvenance(rawClass?.ProvenanceStack);
                    var locFallback = ProvenanceHelper.BuildLocationFromRaw(rawClass?.ProvenanceStack) ?? $"{nsName}.<class>";
                    DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"Failed to transform class '{rawClass!.ClassName ?? "<missing>"}': {aex.Message}", locProv, locFallback);
                }
                catch (Exception ex)
                {
                    var locProv = ProvenanceHelper.MakeProvenance(rawClass?.ProvenanceStack);
                    var locFallback = ProvenanceHelper.BuildLocationFromRaw(rawClass?.ProvenanceStack) ?? $"{nsName}.<class>";
                    DiagnosticsHelper.Add(local, DiagnosticCode.DtoValidationException, $"Unexpected error while transforming class '{rawClass!.ClassName ?? "<missing>"}': {ex.Message}", locProv, locFallback);
                }
            }

            if (raw?.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            rootDiagnostics.AddRange(local);
            if (raw is null) throw new ArgumentNullException(nameof(raw));
            return new NamespaceDto(
                namespaceName: nsName,
                interfaces: interfaces.ToList().AsReadOnly(),
                classes: classes.ToList().AsReadOnly(),
                provenanceStack: ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );
        }

        // Transform a RawInterfaceDto into a canonical InterfaceDto
        private InterfaceDto TransformInterface(RawInterfaceDto raw, string parentNamespace, List<Diagnostic> localDiagnostics)
        {
            var local = new List<Diagnostic>();

            var prov = ProvenanceHelper.MakeProvenance(raw?.ProvenanceStack);
            var locFallback = ProvenanceHelper.BuildLocationFromRaw(raw?.ProvenanceStack) ?? "<unknown>";

            if (string.IsNullOrWhiteSpace(raw?.InterfaceName))
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.InterfaceMissingName, "InterfaceName is required.", prov, locFallback);
            }
            else
            {
                if (!raw.InterfaceName.IsValidIdentifier())
                    DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"InterfaceName '{raw.InterfaceName}' is not a valid identifier.", prov, locFallback);

                if (!raw.InterfaceName.IsInterfaceName())
                    DiagnosticsHelper.Add(local, DiagnosticCode.InterfaceMissingName, $"InterfaceName '{raw.InterfaceName}' does not follow interface naming convention.", prov, locFallback);
            }

            localDiagnostics.AddRange(local);

            var shortName = raw?.InterfaceName ?? "<missing>";
            var qualified = string.IsNullOrWhiteSpace(raw?.InterfaceName) ? "<missing>" : $"{parentNamespace}.{shortName}";
            if (raw is null) throw new ArgumentNullException(nameof(raw));
            return new InterfaceDto(
                interfaceName: shortName,
                qualifiedInterfaceName: qualified,
                provenanceStack: ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );
        }

        // Transform a RawParameterDto into a canonical ParameterDto
        private DdiCodeGen.Dtos.Canonical.ParameterDto TransformParameter(RawParameterDto raw, out IReadOnlyList<Diagnostic> outDiagnostics)
        {
            var local = new List<Diagnostic>();

            if (raw is null)
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.ParameterInvalidNode, "Parameter node is null.", (ProvenanceStack?)null, "<unknown>");
                outDiagnostics = local.ToList().AsReadOnly();
                var provNull = ProvenanceHelper.MakeProvenance((ProvenanceStack?)null, "<unknown>");
                return new ParameterDto(
                    parameterName: "<missing>",
                    qualifiedClassName: null,
                    qualifiedInterfaceName: null,
                    isArray: false,
                    isNullable: false,
                    isValid: false,
                    diagnostics: outDiagnostics,
                    provenanceStack: provNull
                );
            }

            var provRaw = ProvenanceHelper.MakeProvenance(raw.ProvenanceStack);
            var fallbackLocation = ProvenanceHelper.BuildLocationFrom(raw.ProvenanceStack) ?? "<unknown>";
            var prov = ProvenanceHelper.MakeProvenance(provRaw, fallbackLocation);

            try
            {
                var paramName = string.IsNullOrWhiteSpace(raw.ParameterName) ? "<missing>" : raw.ParameterName.Trim();

                if (string.IsNullOrWhiteSpace(raw.ParameterName))
                    DiagnosticsHelper.Add(local, DiagnosticCode.ParameterMissingName, "ParameterName is required.", prov, fallbackLocation);
                else if (!paramName.IsValidIdentifier())
                    DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"ParameterName '{paramName}' is not a valid identifier.", prov, fallbackLocation);

                var hasClassToken = !string.IsNullOrWhiteSpace(raw.QualifiedClassName);
                var hasInterfaceToken = !string.IsNullOrWhiteSpace(raw.QualifiedInterfaceName);

                if (hasClassToken && hasInterfaceToken)
                    DiagnosticsHelper.Add(local, DiagnosticCode.ParameterBothClassAndInterface, "Parameter cannot specify both QualifiedClassName and QualifiedInterfaceName.", prov, fallbackLocation);
                else if (!hasClassToken && !hasInterfaceToken)
                    DiagnosticsHelper.Add(local, DiagnosticCode.ParameterMissingClassOrInterface, "Parameter must specify either QualifiedClassName or QualifiedInterfaceName.", prov, fallbackLocation);

                string? qualifiedClassBase = string.IsNullOrWhiteSpace(raw.QualifiedClassBaseName) ? null : raw.QualifiedClassBaseName.Trim();
                string? qualifiedInterfaceBase = string.IsNullOrWhiteSpace(raw.QualifiedInterfaceBaseName) ? null : raw.QualifiedInterfaceBaseName.Trim();

                if (hasClassToken)
                {
                    if (string.IsNullOrWhiteSpace(qualifiedClassBase))
                        DiagnosticsHelper.Add(local, DiagnosticCode.ParameterMissingQualifiedClass, $"QualifiedClassName '{raw.QualifiedClassName}' is empty after parsing.", prov, fallbackLocation);
                    else if (!qualifiedClassBase.IsQualifiedName())
                        DiagnosticsHelper.Add(local, DiagnosticCode.ParameterMissingQualifiedClass, $"QualifiedClassName '{raw.QualifiedClassName}' has invalid base name '{qualifiedClassBase}'.", prov, fallbackLocation);
                }

                if (hasInterfaceToken)
                {
                    if (string.IsNullOrWhiteSpace(qualifiedInterfaceBase))
                        DiagnosticsHelper.Add(local, DiagnosticCode.ParameterMissingQualifiedInterface, $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' is empty after parsing.", prov, fallbackLocation);
                    else if (!qualifiedInterfaceBase.IsQualifiedName())
                        DiagnosticsHelper.Add(local, DiagnosticCode.ParameterMissingQualifiedInterface, $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' has invalid base name '{qualifiedInterfaceBase}'.", prov, fallbackLocation);
                }

                if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                    local.AddRange(raw.Diagnostics);

                outDiagnostics = local.ToList().AsReadOnly();

                var dto = new ParameterDto(
                    parameterName: paramName,
                    qualifiedClassName: qualifiedClassBase,
                    qualifiedInterfaceName: qualifiedInterfaceBase,
                    isArray: (raw.QualifiedClassIsArray || raw.QualifiedInterfaceIsArray),
                    isNullable: (raw.QualifiedClassIsContainerNullable || raw.QualifiedInterfaceIsContainerNullable),
                    isValid: !outDiagnostics.Any(d => DiagnosticCodeInfo.GetSeverity(d.DiagnosticCode) == DiagnosticSeverity.Error),
                    diagnostics: outDiagnostics,
                    provenanceStack: prov
                );

                return dto;
            }
            catch (Exception ex)
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.DtoValidationException, $"Exception while transforming parameter: {ex.Message}", prov, fallbackLocation);
                outDiagnostics = local.ToList().AsReadOnly();

                return new ParameterDto(
                    parameterName: raw.ParameterName?.Trim() ?? "<missing>",
                    qualifiedClassName: raw.QualifiedClassBaseName,
                    qualifiedInterfaceName: raw.QualifiedInterfaceBaseName,
                    isArray: raw.QualifiedClassIsArray || raw.QualifiedInterfaceIsArray,
                    isNullable: raw.QualifiedClassIsContainerNullable || raw.QualifiedInterfaceIsContainerNullable,
                    isValid: false,
                    diagnostics: outDiagnostics,
                    provenanceStack: prov
                );
            }
        }

        // Transform a RawClassDto into a canonical ClassDto
        private ClassDto TransformClass(RawClassDto raw, string parentNamespace, List<Diagnostic> localDiagnostics)
        {
            var local = new List<Diagnostic>();

            var prov = ProvenanceHelper.MakeProvenance(raw?.ProvenanceStack);
            var locFallback = ProvenanceHelper.BuildLocationFromRaw(raw?.ProvenanceStack) ?? "<unknown>";

            // 1) Short name checks
            if (string.IsNullOrWhiteSpace(raw?.ClassName))
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.ClassMissingName, "ClassName is required.", prov, locFallback);
            }
            else
            {
                if (raw.ClassName.Contains('.') || raw.ClassName.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"ClassName '{raw.ClassName}' must be an unqualified short name; found qualified token. Define the class under the correct namespace instead.", prov, locFallback);
                }

                if (!raw.ClassName.IsValidIdentifier())
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.InvalidIdentifier, $"ClassName '{raw.ClassName}' is not a valid identifier.", prov, locFallback);
                }

                if (!raw.ClassName.IsPascalCase())
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.ClassMissingName, $"ClassName '{raw.ClassName}' does not follow PascalCase convention.", prov, locFallback);
                }
            }

            // 2) Parameters
            var parameterDtos = new List<ParameterDto>();

            // If raw.InitializerParameters is null => the YAML key was missing -> emit error.
            // If it's non-null (even if empty), parse and accept empty list.
            if (raw?.InitializerParameters is null)
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.ClassMissingParameters, $"Missing required 'initializerParameters' for class '{raw?.ClassName ?? "<missing>"}'.", prov, locFallback);
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

            if (raw?.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            // 3) Compose canonical qualified class name (namespace + short name)
            var shortName = raw?.ClassName ?? "<missing>";
            var qualifiedClassName = $"{parentNamespace}.{shortName}";

            if (!qualifiedClassName.IsQualifiedName())
            {
                DiagnosticsHelper.Add(local, DiagnosticCode.NamespaceInvalidSegment, $"Composed qualified class name '{qualifiedClassName}' is not a valid qualified name.", prov, locFallback);
            }

            // 4) Determine effective return type (qualified)
            string effectiveReturnQualified = qualifiedClassName;

            if (!string.IsNullOrWhiteSpace(raw?.QualifiedInterfaceName))
            {
                if (!raw.QualifiedInterfaceName.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(local, DiagnosticCode.InterfaceMissingQualifiedName, $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' is not a valid qualified name.", prov, locFallback);
                }
                else
                {
                    var shortIface = raw.QualifiedInterfaceName.ExtractShortName();
                    if (!shortIface.IsInterfaceName())
                    {
                        DiagnosticsHelper.Add(local, DiagnosticCode.InterfaceMissingQualifiedName, $"QualifiedInterfaceName '{raw.QualifiedInterfaceName}' does not resolve to a valid interface name.", prov, locFallback);
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
                qualifiedInterfaceName: raw?.QualifiedInterfaceName,
                returnTypeQualifiedName: effectiveReturnQualified,
                initializerParameters: parameterDtos.ToList().AsReadOnly(),
                provenanceStack: ProvenanceHelper.MakeProvenance(raw?.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );

            return dto;
        }
    }
}

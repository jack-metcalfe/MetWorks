namespace DdiCodeGen.Dtos
{
    /// <summary>
    /// Core orchestration and normalization for Raw -> Canonical transformation.
    /// This partial contains the public entry point, collision checks, provenance transform,
    /// and small shared helpers used by the other partials.
    /// </summary>
    public sealed partial class Transformer
    {
        /// <summary>
        /// Transform a raw model into a canonical model. This method orchestrates:
        /// - per-node transforms (types and instances)
        /// - collision detection and normalization checks
        /// - aggregation of diagnostics into the returned CanonicalModelDto
        /// </summary>
        public CanonicalModelDto Transform(RawModelDto raw)
        {
            if (raw is null) throw new ArgumentNullException(nameof(raw));

            var rootDiagnostics = new List<Diagnostic>();

            // Transform CodeGen (non-throwing)
            var codeGenDto = TransformCodeGen(raw.CodeGen, rootDiagnostics);

            // Transform namespaces (types)
            var namespaceDtos = new List<NamespaceDto>();
            foreach (var rawNs in raw.Namespaces ?? Array.Empty<RawNamespaceDto>())
            {
                var nsDto = TransformNamespace(rawNs, rootDiagnostics);
                namespaceDtos.Add(nsDto);
            }

            // Transform named instances (instances)
            var namedInstanceDtos = new List<NamedInstanceDto>();
            foreach (var rawNi in raw.NamedInstances ?? Array.Empty<RawNamedInstanceDto>())
            {
                var niDto = TransformNamedInstance(rawNi, rootDiagnostics);
                namedInstanceDtos.Add(niDto);
            }

            // Apply collision detection and other cross-DTO normalization checks
            ApplyCollisionChecks(namespaceDtos, namedInstanceDtos, rootDiagnostics);

            // Validate wiring, invoker keys and named-instance ordering using the raw model as the source of truth for ordering
            ValidateWiringAndOrdering(raw, namespaceDtos, namedInstanceDtos, rootDiagnostics);

            // Build canonical model with aggregated diagnostics
            var canonical = new CanonicalModelDto(
                codeGen: codeGenDto,
                namespaces: namespaceDtos.ToList().AsReadOnly(),
                namedInstances: namedInstanceDtos.ToList().AsReadOnly(),
                sourcePath: raw.SourcePath ?? "<in-memory>",
                provenanceStack: ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                diagnostics: rootDiagnostics.ToList().AsReadOnly()
            );

            return canonical;
        }

        /// <summary>
        /// Transform CodeGen. Non-throwing: returns a placeholder CodeGenDto with diagnostics when fields are missing.
        /// </summary>
        // Place this in RawToCanonicalTransformer (same partial class)
        private static CodeGenDto TransformCodeGen(RawCodeGenDto? raw, List<Diagnostic> rootDiagnostics)
        {
            var local = new List<Diagnostic>();

            if (raw is null)
            {
                var provenanceStack = ProvenanceHelper.MakeProvenance((string?)null, @"Transformer.TransformCodeGen");
                // Missing codeGen section: emit an error diagnostic and return a safe placeholder
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.CodeGenMissingRegistryClass,
                    "Missing codeGen section.",
                    provenanceStack,
                    "<in-memory>#codeGen"
                );

                rootDiagnostics.AddRange(local);

                return new CodeGenDto(
                    registryClassName: "MissingRegistry",
                    generatedCodePath: "<missing>",
                    namespaceName: "Missing.Namespace",
                    initializerName: "MissingInitializer",
                    packageReferences: Array.Empty<PackageReferenceDto>(),
                    provenanceStack: provenanceStack,
                    diagnostics: local
                );
            }

            // Non-null raw: perform structural validation and collect diagnostics
            if (string.IsNullOrWhiteSpace(raw.RegistryClassName))
            {
                var fallbackLocation = "<in-memory>#codeGen.RegistryClassName";
                DiagnosticsHelper.Add(
                    list: local,
                    code: DiagnosticCode.CodeGenMissingRegistryClass,
                    message: "RegistryClassName is required.",
                    provenance: ProvenanceHelper.MakeProvenance((ProvenanceStack?)null, fallbackLocation),
                    fallbackLocation: fallbackLocation
                );
            }
            else
            {
                if (!raw.RegistryClassName.IsValidIdentifier())
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.InvalidIdentifier,
                        $"RegistryClassName '{raw.RegistryClassName}' is not a valid identifier.",
                        ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                    );
                }
                else if (!raw.RegistryClassName.IsPascalCase())
                {
                    DiagnosticsHelper.Add(
                        local,
                        DiagnosticCode.InvalidIdentifier,
                        $"RegistryClassName '{raw.RegistryClassName}' does not follow PascalCase.",
                        ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                    );
                }
            }

            if (string.IsNullOrWhiteSpace(raw.GeneratedCodePath))
            {
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.CodeGenMissingGeneratedPath,
                    "GeneratedCodePath is required.",
                    ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                );
            }

            if (string.IsNullOrWhiteSpace(raw.NamespaceName))
            {
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.CodeGenMissingNamespace,
                    "NamespaceName is required.",
                    ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                );
            }
            else if (!raw.NamespaceName.IsValidNamespace())
            {
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.NamespaceInvalidSegment,
                    $"NamespaceName '{raw.NamespaceName}' is not a valid namespace.",
                    ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                );
            }

            if (string.IsNullOrWhiteSpace(raw.InitializerName))
            {
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.CodeGenMissingInitializer,
                    "InitializerName is required.",
                    ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                );
            }
            else if (!raw.InitializerName.IsValidIdentifier())
            {
                DiagnosticsHelper.Add(
                    local,
                    DiagnosticCode.InvalidIdentifier,
                    $"InitializerName '{raw.InitializerName}' is not a valid identifier.",
                    ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                );
            }

            // Propagate raw diagnostics if any
            if (raw.Diagnostics != null && raw.Diagnostics.Count > 0)
                local.AddRange(raw.Diagnostics);

            // Map package references defensively: ensure non-null read-only list in canonical DTO
            var packageRefs = new List<PackageReferenceDto>();
            if (raw.PackageReferences != null && raw.PackageReferences.Count > 0)
            {
                foreach (var rp in raw.PackageReferences)
                {
                    if (rp == null)
                    {
                        // Unexpected null entry in raw list; warn and continue
                        DiagnosticsHelper.Add(
                            local,
                            DiagnosticCode.InvalidIdentifier,
                            "A packageReference entry was null and was ignored.",
                            ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                            ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                        );
                        continue;
                    }

                    // Expect raw package reference to have Id and Version properties
                    // Map defensively: skip entries without an Id but emit a warning
                    var id = rp.Id;
                    var version = rp.Version;

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        DiagnosticsHelper.Add(
                            local,
                            DiagnosticCode.InvalidIdentifier,
                            "A packageReference entry is missing an Id and was ignored.",
                            ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                            ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                        );
                        continue;
                    }

                    packageRefs.Add(new PackageReferenceDto(id, string.IsNullOrWhiteSpace(version) ? null : version));
                }
            }

            // Add local diagnostics to root
            if (local.Count > 0) rootDiagnostics.AddRange(local);

            // Use placeholders for missing values so the canonical DTO constructor receives valid inputs
            var registry = raw.RegistryClassName ?? "MissingRegistry";
            var generatedPath = raw.GeneratedCodePath ?? "<missing>";
            var nsName = raw.NamespaceName ?? "Missing.Namespace";
            var initializer = raw.InitializerName ?? "MissingInitializer";

            return new CodeGenDto(
                registryClassName: registry,
                generatedCodePath: generatedPath,
                namespaceName: nsName,
                initializerName: initializer,
                packageReferences: packageRefs.ToList().AsReadOnly(),
                provenanceStack: ProvenanceHelper.MakeProvenance(raw.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );
        }

        // --- Collision detection and normalization checks ---
        private void ApplyCollisionChecks(IReadOnlyList<NamespaceDto> namespaces, IReadOnlyList<NamedInstanceDto> namedInstances, List<Diagnostic> rootDiagnostics)
        {
            // 1) Type collisions: (namespace, name) duplicates across classes and interfaces
            var typeEntries = new List<(string Namespace, string Name, object Decl, string FallbackLocation)>();

            foreach (var ns in namespaces ?? Array.Empty<NamespaceDto>())
            {
                var nsName = ns.NamespaceName ?? string.Empty;

                foreach (var c in ns.Classes ?? Array.Empty<ClassDto>())
                {
                    var name = c.ShortName ?? c.ClassName ?? "<missing>";
                    var fallback = $"{nsName}.classes.{name}";
                    typeEntries.Add((nsName, name, (object)c, fallback));
                }

                foreach (var i in ns.Interfaces ?? Array.Empty<InterfaceDto>())
                {
                    var name = i.InterfaceName ?? "<missing>";
                    var fallback = $"{nsName}.interfaces.{name}";
                    typeEntries.Add((nsName, name, (object)i, fallback));
                }
            }

            var collisions = typeEntries
                .GroupBy(t => (t.Namespace, t.Name), new PairComparer())
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in collisions)
            {
                var key = $"{group.Key.Namespace}.{group.Key.Name}";
                foreach (var entry in group)
                {
                    string location;
                    ProvenanceStack? prov = null;
                    switch (entry.Decl)
                    {
                        case ClassDto cd:
                            prov = cd.ProvenanceStack;
                            location = CanonicalHelpers.SafeLocationFromProvenance(cd.ProvenanceStack, entry.FallbackLocation);
                            break;
                        case InterfaceDto id:
                            prov = id.ProvenanceStack;
                            location = CanonicalHelpers.SafeLocationFromProvenance(id.ProvenanceStack, entry.FallbackLocation);
                            break;
                        default:
                            location = entry.FallbackLocation;
                            break;
                    }

                    DiagnosticsHelper.Add(
                        rootDiagnostics,
                        DiagnosticCode.DuplicateTypeIdentifier,
                        $"Type identifier collision: '{key}' is declared multiple times in the same namespace.",
                        prov,
                        entry.FallbackLocation
                    );
                }
            }
            // 2) Named instance collisions: duplicate (namespace, namedInstanceName)
            var instanceEntries = (namedInstances ?? Array.Empty<NamedInstanceDto>())
                .Select(ni =>
                {
                    var ns = "<missing>";
                    try { ns = ni.QualifiedClassName?.SafeExtractNamespace() ?? "<missing>"; } catch { ns = "<missing>"; }
                    var name = ni.NamedInstanceName ?? "<missing>";
                    var fallback = $"{ns}.namedInstances.{name}";
                    return (Namespace: ns, Name: name, Instance: ni, FallbackLocation: fallback);
                })
                .ToList();

            var instanceCollisions = instanceEntries
                .GroupBy(i => (i.Namespace, i.Name), new PairComparer())
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in instanceCollisions)
            {
                var key = $"{group.Key.Namespace}.{group.Key.Name}";
                foreach (var entry in group)
                {
                    var prov = entry.Instance.ProvenanceStack;
                    var fallback = entry.FallbackLocation;
                    DiagnosticsHelper.Add(
                        rootDiagnostics,
                        DiagnosticCode.DuplicateInstanceIdentifier,
                        $"Named instance collision: '{key}' is declared multiple times within the same namespace.",
                        prov,
                        fallback
                    );
                }
            }
        }

        private void ValidateWiringAndOrdering(
            RawModelDto raw,
            IReadOnlyList<NamespaceDto> namespaces,
            IReadOnlyList<NamedInstanceDto> namedInstances,
            List<Diagnostic> rootDiagnostics)
        {
            if (namespaces is null) namespaces = Array.Empty<NamespaceDto>();
            if (namedInstances is null) namedInstances = Array.Empty<NamedInstanceDto>();
            if (rootDiagnostics is null) throw new ArgumentNullException(nameof(rootDiagnostics));
            if (raw is null) throw new ArgumentNullException(nameof(raw));

            // --- 1) Validate invoker keys (uniqueness + identifier validity) ---
            var allClasses = namespaces
                .SelectMany(ns => (ns.Classes ?? Array.Empty<ClassDto>())
                    .Select(c => (Namespace: ns, Class: c)))
                .ToList();

            // Validate each class invoker key for presence and identifier validity
            foreach (var entry in allClasses)
            {
                var cls = entry.Class;
                var ns = entry.Namespace;
                var key = !string.IsNullOrWhiteSpace(cls.InvokerKey) ? cls.InvokerKey : (cls.ClassName ?? string.Empty);
                var locFallback = $"{ns.NamespaceName}.classes.{cls.ClassName ?? "<unknown>"}";
                var locProv = cls.ProvenanceStack;

                if (string.IsNullOrWhiteSpace(key))
                {
                    DiagnosticsHelper.Add(
                        rootDiagnostics,
                        DiagnosticCode.InvalidIdentifier,
                        $"Class in namespace '{ns.NamespaceName}' has empty invoker key or class name.",
                        locProv,
                        locFallback
                    );
                }
                else if (!key.IsValidIdentifier())
                {
                    DiagnosticsHelper.Add(
                        rootDiagnostics,
                        DiagnosticCode.InvalidIdentifier,
                        $"Class invoker key '{key}' in namespace '{ns.NamespaceName}' is not a valid identifier.",
                        locProv,
                        locFallback
                    );
                }
            }

            // Duplicate invoker keys across the entire model
            var invokerGroups = allClasses
                .GroupBy(x => !string.IsNullOrWhiteSpace(x.Class.InvokerKey) ? x.Class.InvokerKey : x.Class.ClassName ?? string.Empty, StringComparer.Ordinal)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1);

            foreach (var g in invokerGroups)
            {
                foreach (var x in g)
                {
                    var locFallback = $"{x.Namespace.NamespaceName}.classes.{x.Class.ClassName ?? "<unknown>"}";
                    var locProv = x.Class.ProvenanceStack;
                    DiagnosticsHelper.Add(
                        rootDiagnostics,
                        DiagnosticCode.DuplicateInvokerKey,
                        $"InvokerKey '{g.Key}' collides across the model and must be unique.",
                        locProv,
                        locFallback
                    );
                }
            }

            // --- 2) Build raw named instance index (author-provided order) ---
            var rawNamedIndex = new Dictionary<string, (RawNamedInstanceDto Raw, int Index)>(StringComparer.Ordinal);
            if (raw?.NamedInstances != null)
            {
                for (int i = 0; i < raw.NamedInstances.Count; i++)
                {
                    var rni = raw.NamedInstances[i];
                    if (!string.IsNullOrWhiteSpace(rni.NamedInstanceName) && !rawNamedIndex.ContainsKey(rni.NamedInstanceName))
                        rawNamedIndex[rni.NamedInstanceName] = (rni, i);
                }
            }

            // Local helper: find raw class DTO by qualified class name
            static RawClassDto? FindRawClassByQualifiedName(RawModelDto rawModel, string qualifiedClassName)
            {
                if (rawModel is null || string.IsNullOrWhiteSpace(qualifiedClassName)) return null;
                try
                {
                    var baseName = qualifiedClassName.ExtractBaseQualifiedName();
                    var lastDot = baseName.LastIndexOf('.');
                    if (lastDot < 0) return null;
                    var nsName = baseName.Substring(0, lastDot);
                    var shortName = baseName.Substring(lastDot + 1);

                    if (rawModel.Namespaces == null) return null;
                    foreach (var rawNs in rawModel.Namespaces)
                    {
                        if (!string.Equals(rawNs.NamespaceName, nsName, StringComparison.Ordinal)) continue;
                        var found = rawNs.Classes?.FirstOrDefault(c => string.Equals(c.ClassName, shortName, StringComparison.Ordinal));
                        if (found != null) return found;
                    }
                }
                catch
                {
                    // swallow and return null for malformed qualified names
                }
                return null;
            }

            // Helper: build lookups for canonical classes and interfaces by qualified name
            var classLookup = new Dictionary<string, ClassDto>(StringComparer.Ordinal);
            var ifaceLookup = new Dictionary<string, InterfaceDto>(StringComparer.Ordinal);
            foreach (var ns in namespaces)
            {
                foreach (var c in ns.Classes ?? Array.Empty<ClassDto>())
                {
                    if (!string.IsNullOrWhiteSpace(c.QualifiedClassName) && !classLookup.ContainsKey(c.QualifiedClassName))
                        classLookup[c.QualifiedClassName] = c;
                }
                foreach (var i in ns.Interfaces ?? Array.Empty<InterfaceDto>())
                {
                    if (!string.IsNullOrWhiteSpace(i.QualifiedInterfaceName) && !ifaceLookup.ContainsKey(i.QualifiedInterfaceName))
                        ifaceLookup[i.QualifiedInterfaceName] = i;
                }
            }

            // --- 3) Validate named instance references, ordering, and wiring ---
            for (int i = 0; i < namedInstances.Count; i++)
            {
                var ni = namedInstances[i];
                var niLocationFallback = $"namedInstances[{i}]";
                var niProv = ni.ProvenanceStack;
                var niLocation = CanonicalHelpers.SafeLocationFromProvenance(niProv, niLocationFallback);

                // Determine this named-instance's raw index (author-provided order)
                var thisIndex = i;
                if (raw != null && raw.NamedInstances != null)
                {
                    var rawNi = raw.NamedInstances.FirstOrDefault(r => string.Equals(r.NamedInstanceName, ni.NamedInstanceName, StringComparison.Ordinal));
                    if (rawNi != null && !string.IsNullOrWhiteSpace(rawNi.NamedInstanceName) && rawNamedIndex.TryGetValue(rawNi.NamedInstanceName, out var entry))
                        thisIndex = entry.Index;
                }

                // Elements referencing other named instances (ordering)
                foreach (var elem in ni.Elements ?? Array.Empty<NamedInstanceElementDto>())
                {
                    if (string.IsNullOrWhiteSpace(elem.AssignmentNamedInstanceName)) continue;

                    if (!rawNamedIndex.TryGetValue(elem.AssignmentNamedInstanceName, out var referenced))
                    {
                        DiagnosticsHelper.Add(
                            rootDiagnostics,
                            DiagnosticCode.NamedInstanceMissing,
                            $"Named instance '{elem.AssignmentNamedInstanceName}' referenced in elements of '{ni.NamedInstanceName}' was not found.",
                            niProv,
                            niLocationFallback
                        );
                        continue;
                    }

                    if (referenced.Index >= thisIndex)
                    {
                        DiagnosticsHelper.Add(
                            rootDiagnostics,
                            DiagnosticCode.DependencyOrderViolation,
                            $"Named instance '{ni.NamedInstanceName}' references '{elem.AssignmentNamedInstanceName}' which appears later in the YAML. Move '{elem.AssignmentNamedInstanceName}' earlier.",
                            niProv,
                            niLocationFallback
                        );
                    }
                }

                // Assignments referencing named instances and wiring validation
                foreach (var assign in ni.Assignments ?? Array.Empty<NamedInstanceAssignmentDto>())
                {
                    if (string.IsNullOrWhiteSpace(assign.AssignmentNamedInstanceName)) continue;

                    if (!rawNamedIndex.TryGetValue(assign.AssignmentNamedInstanceName, out var referenced))
                    {
                        DiagnosticsHelper.Add(
                            rootDiagnostics,
                            DiagnosticCode.NamedInstanceMissing,
                            $"Named instance '{assign.AssignmentNamedInstanceName}' referenced by assignment in '{ni.NamedInstanceName}' was not found.",
                            niProv,
                            niLocationFallback
                        );
                        continue;
                    }

                    if (referenced.Index >= thisIndex)
                    {
                        DiagnosticsHelper.Add(
                            rootDiagnostics,
                            DiagnosticCode.DependencyOrderViolation,
                            $"Named instance '{ni.NamedInstanceName}' references '{assign.AssignmentNamedInstanceName}' which appears later in the YAML. Move '{assign.AssignmentNamedInstanceName}' earlier.",
                            niProv,
                            niLocationFallback
                        );
                    }

                    // Wiring: validate assignment against the target class's initializer parameter definition
                    var targetClassQualified = ni.QualifiedClassName;
                    if (!string.IsNullOrWhiteSpace(targetClassQualified) && classLookup.TryGetValue(targetClassQualified, out var targetClass))
                    {
                        var paramName = assign.AssignmentParameterName;
                        var paramDef = targetClass.InitializerParameters?.FirstOrDefault(p => string.Equals(p.ParameterName, paramName, StringComparison.Ordinal));
                        if (paramDef != null)
                        {
                            // If parameter expects an interface, ensure referenced named instance exposes that interface.
                            if (!string.IsNullOrWhiteSpace(paramDef.QualifiedInterfaceName))
                            {
                                // Find the referenced raw named instance and its raw class to inspect exposed interface
                                var referencedRawNi = referenced.Raw;
                                var referencedQualifiedClass = referencedRawNi?.QualifiedClassName;
                                var referencedRawClass = FindRawClassByQualifiedName(raw!, referencedQualifiedClass ?? string.Empty);

                                var exposedIface = referencedRawClass?.QualifiedInterfaceName;
                                var expectedIfaceBase = paramDef.QualifiedInterfaceName?.ExtractBaseQualifiedName();

                                if (string.IsNullOrWhiteSpace(exposedIface) || !string.Equals(exposedIface.ExtractBaseQualifiedName(), expectedIfaceBase, StringComparison.Ordinal))
                                {
                                    DiagnosticsHelper.Add(
                                        rootDiagnostics,
                                        DiagnosticCode.NamedInstanceMissingOrNotExposingInterface,
                                        $"Named instance '{referenced.Raw?.NamedInstanceName ?? assign.AssignmentNamedInstanceName}' does not expose required interface '{paramDef.QualifiedInterfaceName}' for parameter '{paramDef.ParameterName}' on '{targetClassQualified}'.",
                                        niProv,
                                        niLocationFallback
                                    );
                                }
                            }

                            // If parameter expects a concrete (non-primitive) class, that's invalid: non-primitive parameters must be interfaces
                            if (!string.IsNullOrWhiteSpace(paramDef.QualifiedClassName))
                            {
                                var baseClass = paramDef.QualifiedClassName.ExtractBaseQualifiedName();
                                if (!baseClass.IsPrimitiveQualified())
                                {
                                    DiagnosticsHelper.Add(
                                        rootDiagnostics,
                                        DiagnosticCode.ParameterMustBeInterfaceForNonPrimitive,
                                        $"Parameter '{paramDef.ParameterName}' on '{targetClassQualified}' uses non-primitive concrete type '{paramDef.QualifiedClassName}'. Non-primitive parameters must be interfaces.",
                                        niProv,
                                        niLocationFallback
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }

        // Helper: find a class DTO by its qualified name across namespaces
        private static ClassDto? FindClassByQualifiedName(IReadOnlyList<NamespaceDto> namespaces, string qualifiedName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedName)) return null;
            foreach (var ns in namespaces)
            {
                var found = ns.Classes?.FirstOrDefault(c => string.Equals(c.QualifiedClassName, qualifiedName, StringComparison.Ordinal));
                if (found != null) return found;
            }
            return null;
        }

        // small comparer for (string Namespace, string Name)
        private sealed class PairComparer : IEqualityComparer<(string Namespace, string Name)>
        {
            public bool Equals((string Namespace, string Name) x, (string Namespace, string Name) y) =>
                string.Equals(x.Namespace, y.Namespace, StringComparison.Ordinal) &&
                string.Equals(x.Name, y.Name, StringComparison.Ordinal);

            public int GetHashCode((string Namespace, string Name) obj)
            {
                unchecked
                {
                    var h1 = obj.Namespace != null ? StringComparer.Ordinal.GetHashCode(obj.Namespace) : 0;
                    var h2 = obj.Name != null ? StringComparer.Ordinal.GetHashCode(obj.Name) : 0;
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}

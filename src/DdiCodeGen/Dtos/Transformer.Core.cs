using System;
using System.Collections.Generic;
using System.Linq;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Dtos.Raw;
using DdiCodeGen.Dtos;
using DdiCodeGen.Validation;

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
                provenanceStack: ProvenanceHelper.TransformProvenance(raw.ProvenanceStack),
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
                // Missing codeGen section: emit an error diagnostic and return a safe placeholder
                local.Add(new Diagnostic(
                    DiagnosticCode.CodeGenMissingRegistryClass,
                    "Missing codeGen section.",
                    
                    "<in-memory>#codeGen"
                ));

                rootDiagnostics.AddRange(local);

                return new CodeGenDto(
                    registryClassName: "MissingRegistry",
                    generatedCodePath: "<missing>",
                    namespaceName: "Missing.Namespace",
                    initializerName: "MissingInitializer",
                    packageReferences: Array.Empty<PackageReferenceDto>(),
                    provenanceStack: ProvenanceHelper.MakeProvenance("<in-memory>", "codeGen"),
                    diagnostics: local.ToList().AsReadOnly()
                );
            }

            // Non-null raw: perform structural validation and collect diagnostics
            if (string.IsNullOrWhiteSpace(raw.RegistryClassName))
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.CodeGenMissingRegistryClass,
                    "RegistryClassName is required.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                ));
            }
            else
            {
                if (!raw.RegistryClassName.IsValidIdentifier())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.InvalidIdentifier,
                        $"RegistryClassName '{raw.RegistryClassName}' is not a valid identifier.",
                        
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                    ));
                }
                else if (!raw.RegistryClassName.IsPascalCase())
                {
                    local.Add(new Diagnostic(
                        DiagnosticCode.InvalidIdentifier,
                        $"RegistryClassName '{raw.RegistryClassName}' does not follow PascalCase.",
                        ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                    ));
                }
            }

            if (string.IsNullOrWhiteSpace(raw.GeneratedCodePath))
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.CodeGenMissingGeneratedPath,
                    "GeneratedCodePath is required.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                ));
            }

            if (string.IsNullOrWhiteSpace(raw.NamespaceName))
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.CodeGenMissingNamespace,
                    "NamespaceName is required.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                ));
            }
            else if (!raw.NamespaceName.IsValidNamespace())
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.NamespaceInvalidSegment,
                    $"NamespaceName '{raw.NamespaceName}' is not a valid namespace.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                ));
            }

            if (string.IsNullOrWhiteSpace(raw.InitializerName))
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.CodeGenMissingInitializer,
                    "InitializerName is required.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                ));
            }
            else if (!raw.InitializerName.IsValidIdentifier())
            {
                local.Add(new Diagnostic(
                    DiagnosticCode.InvalidIdentifier,
                    $"InitializerName '{raw.InitializerName}' is not a valid identifier.",
                    
                    ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                ));
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
                        local.Add(new Diagnostic(
                            DiagnosticCode.InvalidIdentifier,
                            "A packageReference entry was null and was ignored.",
                            ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                        ));
                        continue;
                    }

                    // Expect raw package reference to have Id and Version properties
                    // Map defensively: skip entries without an Id but emit a warning
                    var id = rp.Id;
                    var version = rp.Version;

                    if (string.IsNullOrWhiteSpace(id))
                    {
                        local.Add(new Diagnostic(
                            DiagnosticCode.InvalidIdentifier,
                            "A packageReference entry is missing an Id and was ignored.",
                            ProvenanceHelper.BuildLocationFromRaw(raw.ProvenanceStack)
                        ));
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
                provenanceStack: ProvenanceHelper.TransformProvenanceFromRaw(raw.ProvenanceStack),
                diagnostics: local.ToList().AsReadOnly()
            );
        }

        // --- Collision detection and normalization checks ---
        private void ApplyCollisionChecks(IReadOnlyList<NamespaceDto> namespaces, IReadOnlyList<NamedInstanceDto> namedInstances, List<Diagnostic> rootDiagnostics)
        {
            // Type collisions: (namespace, name) duplicates across classes and interfaces
            var typeEntries = new List<(string Namespace, string Name, string Kind, NamespaceDto NamespaceDto, object Decl)>();

            foreach (var ns in namespaces)
            {
                var nsName = ns.NamespaceName ?? string.Empty;
                foreach (var c in ns.Classes ?? Array.Empty<ClassDto>())
                    typeEntries.Add((nsName, c.ClassName, "class", ns, c));
                foreach (var i in ns.Interfaces ?? Array.Empty<InterfaceDto>())
                    typeEntries.Add((nsName, i.InterfaceName, "interface", ns, i));
            }

            var collisions = typeEntries
                .GroupBy(t => (t.Namespace, t.Name))
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in collisions)
            {
                var key = $"{group.Key.Namespace}.{group.Key.Name}";
                foreach (var entry in group)
                {
                    var location = entry.Decl switch
                    {
                        ClassDto cd => cd.ProvenanceStack.Latest.Origin.LogicalPath,
                        InterfaceDto id => id.ProvenanceStack.Latest.Origin.LogicalPath,
                        _ => entry.NamespaceDto.ProvenanceStack.Latest.Origin.LogicalPath
                    };

                    var diag = new Diagnostic(
                        DiagnosticCode.DuplicateTypeIdentifier,
                        $"Type identifier collision: '{key}' is declared multiple times in the same namespace.",
                        
                        location
                    );

                    // Add to root diagnostics. Consumers can map diagnostics to DTOs by location if needed.
                    rootDiagnostics.Add(diag);
                }
            }

            // Named instance collisions: duplicate (namespace, namedInstanceName)
            var instanceEntries = namedInstances
                .Select(ni =>
                {
                    var ns = ni.QualifiedClassName?.ExtractNamespace() ?? string.Empty;
                    return (Namespace: ns, Name: ni.NamedInstanceName, Instance: ni);
                })
                .ToList();

            var instanceCollisions = instanceEntries
                .GroupBy(i => (i.Namespace, i.Name))
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in instanceCollisions)
            {
                var key = $"{group.Key.Namespace}.{group.Key.Name}";
                foreach (var entry in group)
                {
                    var location = entry.Instance.ProvenanceStack.Latest.Origin.LogicalPath;
                    var diag = new Diagnostic(
                        DiagnosticCode.DuplicateInstanceIdentifier,
                        $"Named instance collision: '{key}' is declared multiple times within the same namespace.",
                        
                        location
                    );

                    rootDiagnostics.Add(diag);
                }
            }
        }

        private void ValidateWiringAndOrdering(RawModelDto raw, IReadOnlyList<NamespaceDto> namespaces, IReadOnlyList<NamedInstanceDto> namedInstances, List<Diagnostic> rootDiagnostics)
        {
            if (raw is null) return;

            // 1) Validate class invoker keys (uniqueness across the entire model)
            foreach (var ns in namespaces)
            {
                // Flatten all classes across namespaces with their provenance
                var allClasses = namespaces
                    .SelectMany(ns => (ns.Classes ?? Array.Empty<ClassDto>())
                        .Select(cls => (Namespace: ns, Class: cls)))
                    .ToList();

                // Group by invoker key
                var groups = allClasses
                    .GroupBy(x =>
                    {
                        var key = !string.IsNullOrWhiteSpace(x.Class.InvokerKey)
                            ? x.Class.InvokerKey
                            : (x.Class.ClassName ?? string.Empty);

                        return key;
                    }, StringComparer.Ordinal)
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .ToList();

                // Emit invalid identifier diagnostics for any empty/invalid keys
                foreach (var x in allClasses)
                {
                    var key = !string.IsNullOrWhiteSpace(x.Class.InvokerKey)
                        ? x.Class.InvokerKey
                        : (x.Class.ClassName ?? string.Empty);

                    var loc = x.Class.ProvenanceStack?.Latest?.Origin?.LogicalPath
                              ?? $"{x.Namespace.NamespaceName}.classes.{x.Class.ClassName ?? "<unknown>"}";

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        rootDiagnostics.Add(new Diagnostic(
                            DiagnosticCode.InvalidIdentifier,
                            $"Class in namespace '{x.Namespace.NamespaceName}' has empty identifier.",
                            
                            loc));
                    }
                    else if (!key.IsValidIdentifier())
                    {
                        rootDiagnostics.Add(new Diagnostic(
                            DiagnosticCode.InvalidIdentifier,
                            $"Class identifier '{key}' in namespace '{x.Namespace.NamespaceName}' is not a valid identifier.",
                            
                            loc));
                    }
                }

                // Emit a DuplicateInvokerKey diagnostic for each duplicate occurrence
                foreach (var g in groups.Where(g => g.Count() > 1))
                {
                    foreach (var x in g)
                    {
                        var loc = x.Class.ProvenanceStack?.Latest?.Origin?.LogicalPath
                                  ?? $"{x.Namespace.NamespaceName}.classes.{x.Class.ClassName ?? "<unknown>"}";

                        rootDiagnostics.Add(new Diagnostic(
                            DiagnosticCode.DuplicateInvokerKey,
                            $"InvokerKey '{g.Key}' collides across namespaces. It must be unique across the entire model.",
                            
                            loc));
                    }
                }
            }

            // 2) Build raw named instance index and position map (author-provided order is authoritative)
            var rawNamedIndex = new Dictionary<string, (RawNamedInstanceDto Raw, int Index)>(StringComparer.Ordinal);
            if (raw.NamedInstances != null)
            {
                for (int i = 0; i < raw.NamedInstances.Count; i++)
                {
                    var rni = raw.NamedInstances[i];
                    if (!string.IsNullOrWhiteSpace(rni.NamedInstanceName))
                        rawNamedIndex[rni.NamedInstanceName] = (rni, i);
                }
            }

            // Helper: find raw class DTO by qualified class name (namespace + short name)
            static RawClassDto? FindRawClassByQualifiedName(RawModelDto rawModel, string qualifiedClassName)
            {
                if (string.IsNullOrWhiteSpace(qualifiedClassName)) return null;
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

                return null;
            }

            // 3) Validate named instance references and assignments
            for (int i = 0; i < namedInstances.Count; i++)
            {
                var ni = namedInstances[i];
                var rawNi = raw.NamedInstances?.FirstOrDefault(r => string.Equals(r.NamedInstanceName, ni.NamedInstanceName, StringComparison.Ordinal));
                var niLocation = ni.ProvenanceStack?.Latest?.Origin?.LogicalPath ?? $"namedInstances[{i}]";

                // Determine this named-instance's raw index (author-provided order)
                var thisIndex = i;
                if (rawNi != null && !string.IsNullOrWhiteSpace(rawNi.NamedInstanceName) && rawNamedIndex.TryGetValue(rawNi.NamedInstanceName, out var thisEntry))
                    thisIndex = thisEntry.Index;

                // Elements referencing other named instances (ordering)
                foreach (var elem in ni.Elements ?? Array.Empty<NamedInstanceElementDto>())
                {
                    if (string.IsNullOrWhiteSpace(elem.NamedInstanceName)) continue;
                    if (!rawNamedIndex.TryGetValue(elem.NamedInstanceName, out var referenced))
                    {
                        rootDiagnostics.Add(new Diagnostic(DiagnosticCode.NamedInstanceMissing, $"Named instance '{elem.NamedInstanceName}' referenced in elements of '{ni.NamedInstanceName}' was not found.",  niLocation));
                        continue;
                    }

                    if (referenced.Index >= thisIndex)
                    {
                        rootDiagnostics.Add(new Diagnostic(DiagnosticCode.DependencyOrderViolation, $"Named instance '{ni.NamedInstanceName}' references '{elem.NamedInstanceName}' which appears later in the YAML. Move '{elem.NamedInstanceName}' earlier.",  niLocation));
                    }
                }

                // Assignments referencing named instances
                foreach (var assign in ni.Assignments ?? Array.Empty<NamedInstanceAssignmentDto>())
                {
                    if (string.IsNullOrWhiteSpace(assign.NamedInstanceName)) continue;

                    if (!rawNamedIndex.TryGetValue(assign.NamedInstanceName, out var referenced))
                    {
                        rootDiagnostics.Add(new Diagnostic(DiagnosticCode.NamedInstanceMissing, $"Named instance '{assign.NamedInstanceName}' referenced by assignment in '{ni.NamedInstanceName}' was not found.",  niLocation));
                        continue;
                    }

                    // Ordering: referenced must appear earlier in raw list than this named instance
                    if (referenced.Index >= thisIndex)
                    {
                        rootDiagnostics.Add(new Diagnostic(DiagnosticCode.DependencyOrderViolation, $"Named instance '{ni.NamedInstanceName}' references '{assign.NamedInstanceName}' which appears later in the YAML. Move '{assign.NamedInstanceName}' earlier.",  niLocation));
                    }

                    // Wiring: determine the target class and parameter definition to validate interface exposure
                    var targetClassQualified = ni.QualifiedClassName;
                    if (!string.IsNullOrWhiteSpace(targetClassQualified))
                    {
                        var targetClass = FindClassByQualifiedName(namespaces, targetClassQualified);
                        if (targetClass != null)
                        {
                            var paramName = assign.AssignmentParameterName;
                            var paramDef = targetClass.InitializerParameters?.FirstOrDefault(p => string.Equals(p.ParameterName, paramName, StringComparison.Ordinal));
                            if (paramDef != null)
                            {
                                // If parameter expects an interface, ensure referenced named instance exposes that interface.
                                // RawNamedInstanceDto does not carry QualifiedInterfaceName; find the referenced raw named instance's class and then its raw class DTO to get the interface.
                                var referencedRawNi = referenced.Raw;
                                var referencedQualifiedClass = referencedRawNi?.QualifiedClassName;
                                var referencedRawClass = FindRawClassByQualifiedName(raw, referencedQualifiedClass ?? string.Empty);

                                if (!string.IsNullOrWhiteSpace(paramDef.QualifiedInterfaceName))
                                {
                                    var exposedIface = referencedRawClass?.QualifiedInterfaceName;
                                    if (string.IsNullOrWhiteSpace(exposedIface) || !string.Equals(exposedIface.ExtractBaseQualifiedName(), paramDef.QualifiedInterfaceName, StringComparison.Ordinal))
                                    {
                                        rootDiagnostics.Add(new Diagnostic(DiagnosticCode.NamedInstanceMissingOrNotExposingInterface, $"Named instance '{referenced.Raw.NamedInstanceName}' does not expose required interface '{paramDef.QualifiedInterfaceName}' for parameter '{paramDef.ParameterName}' on '{targetClassQualified}'.",  niLocation));
                                    }
                                }

                                // If parameter expects a primitive (qualifiedClassName is primitive), ensure assignment is a literal or a named instance that wraps a primitive
                                if (!string.IsNullOrWhiteSpace(paramDef.QualifiedClassName))
                                {
                                    var baseClass = paramDef.QualifiedClassName.ExtractBaseQualifiedName();
                                    if (!baseClass.IsPrimitiveQualified())
                                    {
                                        // Non-primitive concrete class parameters should have been rejected earlier; add defensive diagnostic
                                        rootDiagnostics.Add(new Diagnostic(DiagnosticCode.ParameterMustBeInterfaceForNonPrimitive, $"Parameter '{paramDef.ParameterName}' on '{targetClassQualified}' uses non-primitive concrete type '{paramDef.QualifiedClassName}'. Non-primitive parameters must be interfaces.",  niLocation));
                                    }
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
    }
}

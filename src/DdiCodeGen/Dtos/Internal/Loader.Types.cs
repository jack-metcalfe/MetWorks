using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Validation;

namespace DdiCodeGen.Dtos.Internal
{
    internal sealed partial class Loader
    {
        public RawModelDto Load(string yamlText, string sourcePath = "<in-memory>")
        {
            // Normalize input
            yamlText = yamlText?.Trim() ?? string.Empty;

            var diagnostics = new List<Diagnostic>();
            YamlMappingNode? root = null;

            try
            {
                var yaml = new YamlStream();
                using var reader = new StringReader(yamlText);
                yaml.Load(reader);

                if (yaml.Documents.Count == 0)
                {
                    DiagnosticsHelper.Add(diagnostics, DiagnosticCode.UnrecognizedToken, "YAML document is empty.", sourcePath);
                }
                else if (yaml.Documents[0].RootNode is YamlMappingNode mapping)
                {
                    root = mapping;
                }
                else
                {
                    DiagnosticsHelper.Add(diagnostics, DiagnosticCode.UnrecognizedToken, "YAML root must be a mapping node.", sourcePath);
                }
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                // Convert parser exception into a diagnostic with provenance pointing at the document root
                DiagnosticsHelper.Add(diagnostics, DiagnosticCode.UnrecognizedToken, $"YAML parse error: {ex.Message}", sourcePath);
            }
            catch (Exception ex)
            {
                // Unexpected error should also be surfaced as a diagnostic rather than throwing
                DiagnosticsHelper.Add(diagnostics, DiagnosticCode.UnrecognizedToken, $"Unexpected YAML load error: {ex.Message}", sourcePath);
            }

            // If we failed to obtain a mapping root, return an empty RawModelDto with diagnostics
            if (root == null)
            {
                return new RawModelDto(
                    CodeGen: null,
                    Namespaces: Array.Empty<RawNamespaceDto>().ToList().AsReadOnly(),
                    NamedInstances: Array.Empty<RawNamedInstanceDto>().ToList().AsReadOnly(),
                    SourcePath: sourcePath,
                    ProvenanceStack: ProvenanceHelper.MakeRawProvenance(sourcePath, "root"),
                    Diagnostics: diagnostics.ToList().AsReadOnly()
                );
            }

            // Parse the model from the mapping node
            var rawModel = ParseModel(root, sourcePath);

            // Merge any top-level diagnostics we collected during YAML load
            if (diagnostics.Count > 0)
            {
                var merged = rawModel.Diagnostics.ToList();
                merged.InsertRange(0, diagnostics);
                rawModel = rawModel with { Diagnostics = merged.AsReadOnly() }; // or construct a new RawModelDto if not a record
            }

            // Fail fast: do not proceed to transform if there are Error diagnostics
            if (rawModel.Diagnostics.Any(d => d.DiagnosticCode.GetSeverity() == DiagnosticSeverity.Error))
            {
                return rawModel;
            }

            return rawModel;
        }

        private RawModelDto ParseModel(YamlMappingNode root, string sourcePath)
        {
            // Root diagnostics
            var rootDiagnostics = ValidateMappingKeys(root, typeof(RawModelDto), "<root>", sourcePath).ToList();

            // CodeGen
            var codeGenNode = GetChildMapping(root, "codeGen");
            var codeGen = codeGenNode is null
                ? CreateMissingCodeGen(sourcePath, rootDiagnostics)
                : ParseCodeGen(codeGenNode, sourcePath, "codeGen");

            // Namespaces
            var namespacesSeq = GetChildSequence(root, "namespaces");
            var namespaces = new List<RawNamespaceDto>();
            if (namespacesSeq is null)
            {
                rootDiagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamespaceMissingName,
                    "Missing required 'namespaces' section in YAML.",

                    BuildLocation(sourcePath, "namespaces")
                ));
            }
            else
            {
                int idx = 0;
                foreach (var child in namespacesSeq.Children.OfType<YamlMappingNode>())
                {
                    idx++;
                    var nsDto = ParseNamespace(child, sourcePath, $"namespaces[{idx}]");
                    namespaces.Add(nsDto);
                }
            }

            // NamedInstances
            var instancesSeq = GetChildSequence(root, "namedInstances");
            var namedInstances = new List<RawNamedInstanceDto>();
            if (instancesSeq is null)
            {
                // Not necessarily fatal; emit diagnostic and continue
                rootDiagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamedInstanceMissingName,
                    "Missing 'namedInstances' section in YAML.",
                    BuildLocation(sourcePath, "namedInstances")
                ));
            }
            else
            {
                int idx = 0;
                foreach (var child in instancesSeq.Children.OfType<YamlMappingNode>())
                {
                    idx++;
                    var niDto = ParseNamedInstance(child, sourcePath, $"namedInstances[{idx}]");
                    namedInstances.Add(niDto);
                }
            }

            // Aggregate diagnostics from children
            var allDiagnostics = new List<Diagnostic>();
            allDiagnostics.AddRange(rootDiagnostics);
            allDiagnostics.AddRange(codeGen.Diagnostics);
            allDiagnostics.AddRange(namespaces.SelectMany(n => n.Diagnostics));
            allDiagnostics.AddRange(namedInstances.SelectMany(ni => ni.Diagnostics));

            // Construct RawModelDto with defensive copies
            return new RawModelDto(
                CodeGen: codeGen,
                Namespaces: namespaces.ToList().AsReadOnly(),
                NamedInstances: namedInstances.ToList().AsReadOnly(),
                SourcePath: sourcePath,
                ProvenanceStack: MakeProvStack(root, sourcePath, "<root>"),
                Diagnostics: allDiagnostics.ToList().AsReadOnly()
            );
        }

        private RawCodeGenDto ParseCodeGen(YamlMappingNode node, string sourcePath, string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(node, typeof(RawCodeGenDto), logicalPath, sourcePath).ToList();

            var registry = GetScalar(node, "registryClassName");
            var generated = GetScalar(node, "generatedCodePath");
            var ns = GetScalar(node, "namespaceName");
            var init = GetScalar(node, "initializerName");

            // Validate shapes early and attach diagnostics (normalizer will enforce stricter rules)
            if (!string.IsNullOrWhiteSpace(registry) && !registry.IsValidIdentifier())
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.InvalidIdentifier,
                    $"RegistryClassName '{registry}' is not a valid identifier.",

                    BuildLocation(sourcePath, $"{logicalPath}.registryClassName")
                ));

            if (!string.IsNullOrWhiteSpace(ns) && !ns.IsValidNamespace())
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamespaceInvalidSegment,
                    $"NamespaceName '{ns}' is not a valid namespace.",

                    BuildLocation(sourcePath, $"{logicalPath}.namespaceName")
                ));

            if (!string.IsNullOrWhiteSpace(init) && !init.IsValidIdentifier())
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.InvalidIdentifier,
                    $"InitializerName '{init}' is not a valid identifier.",

                    BuildLocation(sourcePath, $"{logicalPath}.initializerName")
                ));

            // Parse optional packageReferences into a non-null list (accept mapping or scalar entries)
            var packageRefs = new List<PackageReferenceDto>();
            if (node.Children.TryGetValue(new YamlScalarNode("packageReferences"), out var prNode))
            {
                if (prNode is YamlSequenceNode seq)
                {
                    var index = 0;
                    foreach (var item in seq.Children)
                    {
                        index++;
                        var itemLocation = BuildLocation(sourcePath, $"{logicalPath}.packageReferences[{index}]");

                        if (item is YamlMappingNode itemMap)
                        {
                            var id = GetScalar(itemMap, "id");
                            var version = GetScalar(itemMap, "version");

                            if (string.IsNullOrWhiteSpace(id))
                            {
                                diagnostics.Add(new Diagnostic(
                                    DiagnosticCode.InvalidIdentifier,
                                    $"packageReferences[{index}] is missing required 'id' and was ignored.",
                                    itemLocation
                                ));
                                continue;
                            }

                            packageRefs.Add(new PackageReferenceDto(id, string.IsNullOrWhiteSpace(version) ? null : version));
                            continue;
                        }

                        if (item is YamlScalarNode scalar)
                        {
                            var text = scalar.Value?.Trim() ?? string.Empty;
                            if (string.IsNullOrEmpty(text))
                            {
                                diagnostics.Add(new Diagnostic(
                                    DiagnosticCode.InvalidIdentifier,
                                    $"packageReferences[{index}] is empty and was ignored.",
                                    itemLocation
                                ));
                                continue;
                            }

                            var colon = text.IndexOf(':');
                            if (colon <= 0)
                            {
                                // only id provided
                                packageRefs.Add(new PackageReferenceDto(text, null));
                            }
                            else
                            {
                                var id = text.Substring(0, colon).Trim();
                                var version = text.Substring(colon + 1).Trim();
                                if (string.IsNullOrWhiteSpace(id))
                                {
                                    diagnostics.Add(new Diagnostic(
                                        DiagnosticCode.InvalidIdentifier,
                                        $"packageReferences[{index}] has an empty id and was ignored.",
                                        itemLocation
                                    ));
                                    continue;
                                }
                                packageRefs.Add(new PackageReferenceDto(id, string.IsNullOrWhiteSpace(version) ? null : version));
                            }

                            continue;
                        }

                        // Unknown node type inside sequence
                        diagnostics.Add(new Diagnostic(
                            DiagnosticCode.InvalidIdentifier,
                            $"packageReferences[{index}] has an unsupported YAML node type and was ignored.",
                            itemLocation
                        ));
                    }
                }
                else
                {
                    // packageReferences present but not a sequence
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.InvalidIdentifier,
                        "The 'packageReferences' entry must be a sequence.",
                        BuildLocation(sourcePath, $"{logicalPath}.packageReferences")
                    ));
                }
            }

            var prov = MakeProvStack(node, sourcePath, logicalPath);

            return new RawCodeGenDto(
                RegistryClassName: registry,
                GeneratedCodePath: generated,
                NamespaceName: ns,
                InitializerName: init,
                PackageReferences: packageRefs.ToList().AsReadOnly(),
                ProvenanceStack: prov,
                Diagnostics: diagnostics.ToList().AsReadOnly()
            );
        }

        private RawNamespaceDto ParseNamespace(YamlMappingNode node, string sourcePath, string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(node, typeof(RawNamespaceDto), logicalPath, sourcePath).ToList();

            // --- NamespaceName ---
            var nsName = GetScalar(node, "namespaceName");
            if (string.IsNullOrWhiteSpace(nsName))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamespaceMissingName,
                    $"Missing required 'namespaceName' in {logicalPath}.",

                    BuildLocation(sourcePath, $"{logicalPath}.namespaceName")));
                nsName = "<missing.namespace>";
            }
            else if (!nsName.IsValidNamespace())
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.NamespaceInvalidSegment,
                    $"Namespace '{nsName}' is not a valid dot-separated namespace.",

                    BuildLocation(sourcePath, $"{logicalPath}.namespaceName")));
            }

            // --- Interfaces ---
            var interfaces = GetInterfaceTokens(node, "interfaces", sourcePath, $"{logicalPath}.interfaces").ToList();

            // --- Classes ---
            var classes = new List<RawClassDto>();
            var classesSeq = GetChildSequence(node, "classes");
            if (classesSeq is null)
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.ClassMissingName,
                    $"Missing 'classes' section in {logicalPath}.",

                    BuildLocation(sourcePath, $"{logicalPath}.classes")));
            }
            else
            {
                int idx = 0;
                foreach (var child in classesSeq.Children.OfType<YamlMappingNode>())
                {
                    idx++;
                    var rawClass = ParseClass(child, sourcePath, $"{logicalPath}.classes[{idx}]");
                    classes.Add(rawClass);

                    // propagate any diagnostics from ParseClass
                    if (rawClass.Diagnostics != null && rawClass.Diagnostics.Count > 0)
                        diagnostics.AddRange(rawClass.Diagnostics);
                }
            }

            // --- Build RawNamespaceDto ---
            return new RawNamespaceDto(
                NamespaceName: nsName,
                Interfaces: interfaces.AsReadOnly(),
                Classes: classes.AsReadOnly(),
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        private RawClassDto ParseClass(YamlMappingNode node, string sourcePath, string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(node, typeof(RawClassDto), logicalPath, sourcePath).ToList();

            // --- initializerParameters may contain mapping nodes or scalar shorthand ---
            var initializerParameters = GetChildSequence(node, "initializerParameters")?
                .Children
                .Select((child, i) =>
                {
                    var childLogical = $"{logicalPath}.initializerParameters[{i}]";
                    if (child is YamlMappingNode map)
                        return ParseParameter(map, sourcePath, childLogical);

                    if (child is YamlScalarNode scalar)
                    {
                        var prov = MakeProvStack(scalar, sourcePath, childLogical);
                        var diags = new List<Diagnostic>();

                        if (string.IsNullOrWhiteSpace(scalar.Value))
                        {
                            diags.Add(new Diagnostic(
                                DiagnosticCode.ParameterMissingName,
                                $"Empty parameter scalar at {childLogical}.",

                                BuildLocation(sourcePath, childLogical)));
                        }
                        else if (!scalar.Value.IsValidIdentifier())
                        {
                            diags.Add(new Diagnostic(
                                DiagnosticCode.InvalidIdentifier,
                                $"Parameter scalar '{scalar.Value}' is not a valid identifier.",

                                BuildLocation(sourcePath, childLogical)));
                        }

                        return new RawParameterDto(
                            ParameterName: scalar.Value,
                            QualifiedClassName: null,
                            QualifiedInterfaceName: null,
                            QualifiedClassBaseName: null,
                            QualifiedClassIsArray: false,
                            QualifiedClassIsContainerNullable: false,
                            QualifiedClassElementIsNullable: false,
                            QualifiedInterfaceBaseName: null,
                            QualifiedInterfaceIsArray: false,
                            QualifiedInterfaceIsContainerNullable: false,
                            QualifiedInterfaceElementIsNullable: false,
                            ProvenanceStack: prov,
                            Diagnostics: diags.AsReadOnly()
                        );
                    }

                    var provFallback = MakeProvStack(child, sourcePath, childLogical);
                    return new RawParameterDto(
                        ParameterName: "<missing>",
                        QualifiedClassName: null,
                        QualifiedInterfaceName: null,
                        QualifiedClassBaseName: null,
                        QualifiedClassIsArray: false,
                        QualifiedClassIsContainerNullable: false,
                        QualifiedClassElementIsNullable: false,
                        QualifiedInterfaceBaseName: null,
                        QualifiedInterfaceIsArray: false,
                        QualifiedInterfaceIsContainerNullable: false,
                        QualifiedInterfaceElementIsNullable: false,
                        ProvenanceStack: provFallback,
                        Diagnostics: new[]
                        {
                    new Diagnostic(
                        DiagnosticCode.UnrecognizedToken,
                        "Unexpected node type in initializerParameters",

                        BuildLocation(sourcePath, childLogical))
                        }.ToList().AsReadOnly()
                    );
                })
                .ToList()
                ?? new List<RawParameterDto>();

            // --- ClassName ---
            var className = GetScalar(node, "className");
            if (string.IsNullOrWhiteSpace(className))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.ClassMissingName,
                    $"Missing required 'className' in {logicalPath}.",

                    BuildLocation(sourcePath, $"{logicalPath}.className")));
                className = "<missing.class>";
            }
            else if (!className.IsValidIdentifier())
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.InvalidIdentifier,
                    $"ClassName '{className}' is not a valid identifier.",

                    BuildLocation(sourcePath, $"{logicalPath}.className")));
            }
            else if (!className.IsPascalCase())
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.ClassMissingName,
                    $"Class name '{className}' does not follow PascalCase convention.",
                    BuildLocation(sourcePath, $"{logicalPath}.className")));
            }

            // --- QualifiedInterfaceName ---
            var rawQInterface = GetScalar(node, "qualifiedInterfaceName");
            string? baseQInterface = null;
            bool ifaceIsArray = false, ifaceIsContainerNullable = false, ifaceIsElementNullable = false;

            if (!string.IsNullOrWhiteSpace(rawQInterface))
            {
                if (rawQInterface.IsNullToken())
                {
                    // Treat "null" as intentionally missing
                    rawQInterface = null;
                }
                else if (!rawQInterface.TryParseTypeRef(out baseQInterface, out ifaceIsArray, out ifaceIsContainerNullable, out ifaceIsElementNullable))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{rawQInterface}' at {logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', 'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside arrays (e.g., 'Ns.Type?[]') are not supported.",

                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")));

                    baseQInterface = null;
                    ifaceIsArray = false;
                    ifaceIsContainerNullable = false;
                    ifaceIsElementNullable = false;
                }
                else if (string.IsNullOrWhiteSpace(baseQInterface) || !baseQInterface.IsQualifiedName())
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.InterfaceMissingQualifiedName,
                        $"QualifiedInterfaceName '{rawQInterface}' is not a valid qualified name after parsing.",

                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")));
                }
            }

            // --- Aggregate parameter diagnostics into class diagnostics ---
            diagnostics.AddRange(initializerParameters.SelectMany(p => p.Diagnostics));

            // --- Build RawClassDto ---
            return new RawClassDto(
                ClassName: className,
                QualifiedInterfaceName: rawQInterface,
                InitializerParameters: initializerParameters.AsReadOnly(),
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        // --- YamlRawModelLoader.Types.cs (excerpt with updated ParseParameter) ---

        private RawParameterDto ParseParameter(YamlMappingNode node, string sourcePath, string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(node, typeof(RawParameterDto), logicalPath, sourcePath).ToList();

            // --- ParameterName ---
            var paramName = GetScalar(node, "parameterName");
            if (string.IsNullOrWhiteSpace(paramName))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.ParameterMissingName,
                    $"Missing 'parameterName' in {logicalPath}.",

                    BuildLocation(sourcePath, $"{logicalPath}.parameterName")));
                paramName = "<missing.param>";
            }
            else if (!paramName.IsValidIdentifier())
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.InvalidIdentifier,
                    $"ParameterName '{paramName}' is not a valid identifier.",

                    BuildLocation(sourcePath, $"{logicalPath}.parameterName")));
            }

            // --- QualifiedClassName / QualifiedInterfaceName ---
            var qClass = GetScalar(node, "qualifiedClassName");
            var qInterface = GetScalar(node, "qualifiedInterfaceName");

            if (!string.IsNullOrWhiteSpace(qClass) && !string.IsNullOrWhiteSpace(qInterface))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.ParameterBothClassAndInterface,
                    $"Parameter at {logicalPath} specifies both qualifiedClassName and qualifiedInterfaceName.",

                    BuildLocation(sourcePath, logicalPath)));
            }
            else if (string.IsNullOrWhiteSpace(qClass) && string.IsNullOrWhiteSpace(qInterface))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCode.ParameterMissingClassOrInterface,
                    $"Parameter at {logicalPath} must specify either qualifiedClassName or qualifiedInterfaceName.",

                    BuildLocation(sourcePath, logicalPath)));
            }

            // --- Parse modifiers deterministically (use TryParseTypeRef) ---
            string? baseQClass = null, baseQInterface = null;
            bool classIsArray = false, classIsContainerNullable = false, classIsElementNullable = false;
            bool ifaceIsArray = false, ifaceIsContainerNullable = false, ifaceIsElementNullable = false;

            if (!string.IsNullOrWhiteSpace(qClass))
            {
                if (!qClass.TryParseTypeRef(out baseQClass, out classIsArray, out classIsContainerNullable, out classIsElementNullable))
                {
                    AddDiagnostic(diagnostics,
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{qClass}' at {logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', 'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside arrays (e.g., 'Ns.Type?[]') are not supported.",

                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName"));

                    // safe defaults
                    baseQClass = null;
                    classIsArray = false;
                    classIsContainerNullable = false;
                    classIsElementNullable = false;
                }
                else if (string.IsNullOrWhiteSpace(baseQClass) || !baseQClass.IsQualifiedName())
                {
                    AddDiagnostic(diagnostics,
                        DiagnosticCode.ParameterMissingQualifiedClass,
                        $"QualifiedClassName '{qClass}' is not a valid qualified name after parsing.",

                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName"));
                }
                else
                {
                    // Enforce: non-primitive concrete classes are not allowed for parameters (must use interface)
                    if (!baseQClass.IsPrimitiveQualified())
                    {
                        AddDiagnostic(diagnostics,
                            DiagnosticCode.ParameterMustBeInterfaceForNonPrimitive,
                            $"Parameter '{paramName}' at {logicalPath} uses non-primitive qualifiedClassName '{qClass}'. Non-primitive parameters must be declared as interfaces (qualifiedInterfaceName).",

                            BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName"));
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(qInterface))
            {
                if (!qInterface.TryParseTypeRef(out baseQInterface, out ifaceIsArray, out ifaceIsContainerNullable, out ifaceIsElementNullable))
                {
                    AddDiagnostic(diagnostics,
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{qInterface}' at {logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', 'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside arrays (e.g., 'Ns.Type?[]') are not supported.",

                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName"));

                    baseQInterface = null;
                    ifaceIsArray = false;
                    ifaceIsContainerNullable = false;
                    ifaceIsElementNullable = false;
                }
                else if (string.IsNullOrWhiteSpace(baseQInterface) || !baseQInterface.IsQualifiedName())
                {
                    AddDiagnostic(diagnostics,
                        DiagnosticCode.ParameterMissingQualifiedInterface,
                        $"QualifiedInterfaceName '{qInterface}' is not a valid qualified name after parsing.",

                        BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName"));
                }
            }

            // --- Build RawParameterDto with parsed fields ---
            return new RawParameterDto(
                ParameterName: paramName,
                QualifiedClassName: qClass,
                QualifiedInterfaceName: qInterface,
                QualifiedClassBaseName: baseQClass,
                QualifiedClassIsArray: classIsArray,
                QualifiedClassIsContainerNullable: classIsContainerNullable,
                QualifiedClassElementIsNullable: classIsElementNullable,
                QualifiedInterfaceBaseName: baseQInterface,
                QualifiedInterfaceIsArray: ifaceIsArray,
                QualifiedInterfaceIsContainerNullable: ifaceIsContainerNullable,
                QualifiedInterfaceElementIsNullable: ifaceIsElementNullable,
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        // Helper to create a placeholder CodeGen when the section is missing
        private RawCodeGenDto CreateMissingCodeGen(string? sourcePath, List<Diagnostic> diagnostics)
        {
            var location = BuildLocation(sourcePath, "codeGen");
            var diag = new Diagnostic(
                DiagnosticCode.CodeGenMissingRegistryClass,
                "Missing 'codeGen' section in YAML.",

                location
            );

            // Add to the caller's diagnostics collection so the root aggregation sees it
            diagnostics.Add(diag);

            // Create a minimal provenance stack for the missing section using named args
            var origin = new RawProvenanceOrigin(
                SourcePath: sourcePath ?? "<in-memory>",
                LineZeroBased: 0,
                ColumnZeroBased: 0,
                LogicalPath: "codeGen"
            );

            var entry = new RawProvenanceEntry(origin, "parser", "yaml-raw-loader", DateTimeOffset.UtcNow);
            var prov = new RawProvenanceStack(Version: 1, Entries: new List<RawProvenanceEntry> { entry });

            // Return a RawCodeGenDto with its own diagnostics copy, an empty packageReferences list, and provenance
            return new RawCodeGenDto(
                RegistryClassName: null,
                GeneratedCodePath: null,
                NamespaceName: null,
                InitializerName: null,
                PackageReferences: Array.Empty<PackageReferenceDto>().ToList().AsReadOnly(),
                ProvenanceStack: prov,
                Diagnostics: new[] { diag }.ToList().AsReadOnly()
            );
        }

        // Extract a namespace-like prefix from a logical path (best-effort)
        private static string ExtractNamespaceFromLogical(string logicalPath)
        {
            // logicalPath like "namespaces[1].classes[2]" -> "namespaces[1]"
            var parts = logicalPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : "MissingNamespace";
        }
    }
}

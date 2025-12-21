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
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.UnrecognizedToken,
                        "YAML root must be a mapping node.",
                        fallbackLocation: sourcePath
                    );
                }
                else if (yaml.Documents[0].RootNode is YamlMappingNode mapping)
                {
                    root = mapping;
                }
                else
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.UnrecognizedToken,
                        "YAML root must be a mapping node.",
                        fallbackLocation: sourcePath
                    );
                }
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                // Convert parser exception into a diagnostic with provenance pointing at the document root
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.UnrecognizedToken,
                    $"YAML parse error: {ex.Message}",
                    fallbackLocation: sourcePath
                );
            }
            catch (Exception ex)
            {
                // Unexpected error should also be surfaced as a diagnostic rather than throwing
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.UnrecognizedToken,
                    $"Unexpected YAML load error: {ex.Message}",
                    fallbackLocation: sourcePath
                );
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

        private RawModelDto ParseModel(
            YamlMappingNode root,
            string sourcePath)
        {
            // --- Root diagnostics ---
            var rootDiagnostics = ValidateMappingKeys(
                root,
                typeof(RawModelDto),
                "<root>",
                sourcePath).ToList();

            // --- CodeGen ---
            var codeGenNode = GetChildMapping(root, "codeGen");
            var codeGen = codeGenNode is null
                ? CreateMissingCodeGen(sourcePath, rootDiagnostics)
                : ParseCodeGen(codeGenNode, sourcePath, "codeGen");

            // --- Namespaces ---
            var namespacesSeq = GetChildSequence(root, "namespaces");
            var namespaces = new List<RawNamespaceDto>();
            if (namespacesSeq is null)
            {
                DiagnosticsHelper.Add(
                    rootDiagnostics,
                    DiagnosticCode.NamespaceMissingName,
                    "Missing required 'namespaces' section in YAML.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, "namespaces"),
                    fallbackLocation: BuildLocation(sourcePath, "namespaces")
                );
            }
            else
            {
                for (int i = 0; i < namespacesSeq.Children.Count; i++)
                {
                    var childLogical = $"namespaces[{i}]";
                    if (namespacesSeq.Children[i] is YamlMappingNode map)
                    {
                        namespaces.Add(ParseNamespace(map, sourcePath, childLogical));
                    }
                    else
                    {
                        var prov = MakeProvStack(namespacesSeq.Children[i],
                                                 sourcePath,
                                                 childLogical);
                        var diags = new List<Diagnostic>();
                        DiagnosticsHelper.Add(
                            diags,
                            DiagnosticCode.NamespaceInvalidNode,
                            $"Namespace at {childLogical} must be a mapping node.",
                            provenance: prov,
                            fallbackLocation: BuildLocation(sourcePath, childLogical)
                        );

                        namespaces.Add(new RawNamespaceDto(
                            NamespaceName: "<invalid.namespace>",
                            Interfaces: Array.Empty<RawInterfaceDto>(),
                            Classes: Array.Empty<RawClassDto>(),
                            ProvenanceStack: prov,
                            Diagnostics: diags.AsReadOnly()
                        ));
                    }
                }
            }

            // --- NamedInstances ---
            var instancesSeq = GetChildSequence(root, "namedInstances");
            var namedInstances = new List<RawNamedInstanceDto>();
            if (instancesSeq is null)
            {
                DiagnosticsHelper.Add(
                    rootDiagnostics,
                    DiagnosticCode.NamedInstanceMissingName,
                    "Missing 'namedInstances' section in YAML.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, "namedInstances"),
                    fallbackLocation: BuildLocation(sourcePath, "namedInstances")
                );
            }
            else
            {
                for (int i = 0; i < instancesSeq.Children.Count; i++)
                {
                    var childLogical = $"namedInstances[{i}]";
                    if (instancesSeq.Children[i] is YamlMappingNode map)
                    {
                        namedInstances.Add(
                            ParseNamedInstance(map, sourcePath, childLogical));
                    }
                    else
                    {
                        var prov = MakeProvStack(instancesSeq.Children[i],
                                                 sourcePath,
                                                 childLogical);
                        var diags = new List<Diagnostic>();
                        DiagnosticsHelper.Add(
                            diags,
                            DiagnosticCode.NamedInstanceInvalidNode,
                            $"NamedInstance at {childLogical} must be a mapping node.",
                            provenance: prov,
                            fallbackLocation: BuildLocation(sourcePath, childLogical)
                        );

                        namedInstances.Add(new RawNamedInstanceDto(
                            NamedInstanceName: "<invalid.instance>",
                            QualifiedClassName: null,
                            QualifiedClassBaseName: null,
                            QualifiedClassIsArray: false,
                            QualifiedClassIsContainerNullable: false,
                            QualifiedClassElementIsNullable: false,
                            QualifiedInterfaceName: null,
                            QualifiedInterfaceBaseName: null,
                            QualifiedInterfaceIsArray: false,
                            QualifiedInterfaceIsContainerNullable: false,
                            QualifiedInterfaceElementIsNullable: false,
                            Assignments: Array.Empty<RawNamedInstanceAssignmentDto>(),
                            Elements: Array.Empty<RawNamedInstanceElementDto>(),
                            ProvenanceStack: prov,
                            Diagnostics: diags.AsReadOnly()
                        ));
                    }
                }
            }

            // --- Aggregate diagnostics ---
            var allDiagnostics = new List<Diagnostic>();
            allDiagnostics.AddRange(rootDiagnostics);
            allDiagnostics.AddRange(codeGen.Diagnostics);
            allDiagnostics.AddRange(namespaces.SelectMany(n => n.Diagnostics));
            allDiagnostics.AddRange(namedInstances.SelectMany(ni => ni.Diagnostics));

            // --- Construct RawModelDto ---
            return new RawModelDto(
                CodeGen: codeGen,
                Namespaces: namespaces.AsReadOnly(),
                NamedInstances: namedInstances.AsReadOnly(),
                SourcePath: sourcePath,
                ProvenanceStack: MakeProvStack(root, sourcePath, "<root>"),
                Diagnostics: allDiagnostics.AsReadOnly()
            );
        }
        // Place this inside Loader (same partial) near ParseModel / ParseCodeGen
        private RawCodeGenDto CreateMissingCodeGen(string sourcePath, List<Diagnostic> rootDiagnostics)
        {
            var diags = new List<Diagnostic>();

            var logical = "codeGen";
            var fallback = BuildLocation(sourcePath, logical);
            var prov = ProvenanceHelper.MakeRawProvenance(sourcePath, logical);

            DiagnosticsHelper.Add(
                diags,
                DiagnosticCode.CodeGenMissingRegistryClass,
                "Missing codeGen section.",
                provenance: prov,
                fallbackLocation: fallback
            );

            // Propagate to the caller's root diagnostics
            if (diags.Count > 0) rootDiagnostics.AddRange(diags);

            // Construct a safe placeholder RawCodeGenDto so parsing can continue
            return new RawCodeGenDto(
                RegistryClassName: null,
                GeneratedCodePath: null,
                NamespaceName: null,
                InitializerName: null,
                PackageReferences: new List<PackageReferenceDto>().AsReadOnly(),
                ProvenanceStack: prov,
                Diagnostics: diags.AsReadOnly()
            );
        }

        private RawCodeGenDto ParseCodeGen(
            YamlMappingNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(
                node,
                typeof(RawCodeGenDto),
                logicalPath,
                sourcePath).ToList();

            var registry = GetScalar(node, "registryClassName");
            var generated = GetScalar(node, "generatedCodePath");
            var ns = GetScalar(node, "namespaceName");
            var init = GetScalar(node, "initializerName");

            // --- Validate identifiers ---
            if (!string.IsNullOrWhiteSpace(registry) &&
                !registry.IsValidIdentifier())
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.InvalidIdentifier,
                    $"RegistryClassName '{registry}' is not a valid identifier.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, $"{logicalPath}.registryClassName"),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.registryClassName")
                );
            }

            if (!string.IsNullOrWhiteSpace(ns) &&
                !ns.IsValidNamespace())
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.NamespaceInvalidSegment,
                    $"NamespaceName '{ns}' is not a valid namespace.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, $"{logicalPath}.namespaceName"),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.namespaceName")
                );
            }

            if (!string.IsNullOrWhiteSpace(init) &&
                !init.IsValidIdentifier())
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.InvalidIdentifier,
                    $"InitializerName '{init}' is not a valid identifier.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, $"{logicalPath}.initializerName"),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.initializerName")
                );
            }

            // --- PackageReferences ---
            var packageRefs = new List<PackageReferenceDto>();
            if (node.Children.TryGetValue(
                new YamlScalarNode("packageReferences"),
                out var prNode))
            {
                if (prNode is YamlSequenceNode seq)
                {
                    for (int i = 0; i < seq.Children.Count; i++)
                    {
                        var itemLogical =
                            $"{logicalPath}.packageReferences[{i}]";
                        var itemLocation =
                            BuildLocation(sourcePath, itemLogical);

                        if (seq.Children[i] is YamlMappingNode itemMap)
                        {
                            var id = GetScalar(itemMap, "id");
                            var version = GetScalar(itemMap, "version");

                            if (string.IsNullOrWhiteSpace(id))
                            {
                                DiagnosticsHelper.Add(
                                    diagnostics,
                                    DiagnosticCode.InvalidIdentifier,
                                    $"packageReferences[{i}] is missing required 'id' and was ignored.",
                                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, itemLogical),
                                    fallbackLocation: itemLocation
                                );
                                continue;
                            }

                            packageRefs.Add(new PackageReferenceDto(
                                id,
                                string.IsNullOrWhiteSpace(version)
                                    ? null
                                    : version));
                            continue;
                        }

                        if (seq.Children[i] is YamlScalarNode scalar)
                        {
                            var text = scalar.Value?.Trim() ?? string.Empty;
                            if (string.IsNullOrEmpty(text))
                            {
                                DiagnosticsHelper.Add(
                                    diagnostics,
                                    DiagnosticCode.InvalidIdentifier,
                                    $"packageReferences[{i}] is empty and was ignored.",
                                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, itemLogical),
                                    fallbackLocation: itemLocation
                                );
                                continue;
                            }

                            var colon = text.IndexOf(':');
                            if (colon <= 0)
                            {
                                packageRefs.Add(
                                    new PackageReferenceDto(text, null));
                            }
                            else
                            {
                                var id = text.Substring(0, colon).Trim();
                                var version = text.Substring(colon + 1).Trim();
                                if (string.IsNullOrWhiteSpace(id))
                                {
                                    DiagnosticsHelper.Add(
                                        diagnostics,
                                        DiagnosticCode.InvalidIdentifier,
                                        $"packageReferences[{i}] has an empty id and was ignored.",
                                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, itemLogical),
                                        fallbackLocation: itemLocation
                                    );
                                    continue;
                                }
                                packageRefs.Add(new PackageReferenceDto(
                                    id,
                                    string.IsNullOrWhiteSpace(version)
                                        ? null
                                        : version));
                            }
                            continue;
                        }

                        // Unknown node type inside sequence
                        DiagnosticsHelper.Add(
                            diagnostics,
                            DiagnosticCode.InvalidIdentifier,
                            $"packageReferences[{i}] has an unsupported YAML node type and was ignored.",
                            provenance: ProvenanceHelper.MakeProvenance(sourcePath, itemLogical),
                            fallbackLocation: itemLocation
                        );
                    }
                }
                else
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.InvalidIdentifier,
                        "The 'packageReferences' entry must be a sequence.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, $"{logicalPath}.packageReferences"),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.packageReferences")
                    );
                }
            }

            var prov = MakeProvStack(node, sourcePath, logicalPath);

            return new RawCodeGenDto(
                RegistryClassName: registry,
                GeneratedCodePath: generated,
                NamespaceName: ns,
                InitializerName: init,
                PackageReferences: packageRefs.AsReadOnly(),
                ProvenanceStack: prov,
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        private RawNamespaceDto ParseNamespace(
            YamlMappingNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(
                node,
                typeof(RawNamespaceDto),
                logicalPath,
                sourcePath).ToList();

            // --- NamespaceName (SimpleName only) ---
            var nsName = GetScalar(node, "namespaceName");
            if (string.IsNullOrWhiteSpace(nsName))
            {
                var loc = BuildLocation(sourcePath, $"{logicalPath}.namespaceName");
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.NamespaceMissingName,
                    $"Missing 'namespaceName' in {logicalPath}.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: loc
                );
                nsName = "<missing.namespace>";
            }
            else if (!nsName.IsValidIdentifier())
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.InvalidIdentifier,
                    $"NamespaceName '{nsName}' must be a simple identifier.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.namespaceName")
                );
            }
            else if (nsName.Contains("."))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.NamespaceNameMustBeSimple,
                    $"NamespaceName '{nsName}' must not contain a namespace.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.namespaceName")
                );
            }

            // --- Interfaces ---
            var interfaces = GetInterfaceTokens(
                node,
                "interfaces",
                sourcePath,
                $"{logicalPath}.interfaces");

            // --- Classes ---
            var classes = new List<RawClassDto>();
            if (node.Children.TryGetValue(new YamlScalarNode("classes"), out var value) &&
                value is YamlSequenceNode seq)
            {
                for (int i = 0; i < seq.Children.Count; i++)
                {
                    var childLogical = $"{logicalPath}.classes[{i}]";
                    if (seq.Children[i] is YamlMappingNode map)
                    {
                        classes.Add(ParseClass(map, sourcePath, childLogical));
                    }
                    else
                    {
                        var prov = MakeProvStack(seq.Children[i], sourcePath,
                                                 childLogical);
                        var diags = new List<Diagnostic>();
                        DiagnosticsHelper.Add(
                            diags,
                            DiagnosticCode.ClassInvalidNode,
                            $"Class at {childLogical} must be a mapping node.",
                            provenance: prov,
                            fallbackLocation: BuildLocation(sourcePath, childLogical)
                        );

                        classes.Add(new RawClassDto(
                            ClassName: "<invalid.class>",
                            QualifiedInterfaceName: null,
                            InitializerParameters: Array.Empty<RawParameterDto>(),
                            ProvenanceStack: prov,
                            Diagnostics: diags.AsReadOnly()
                        ));
                    }
                }
            }

            // --- Aggregate child diagnostics ---
            diagnostics.AddRange(interfaces.SelectMany(i => i.Diagnostics));
            diagnostics.AddRange(classes.SelectMany(c => c.Diagnostics));

            return new RawNamespaceDto(
                NamespaceName: nsName,
                Interfaces: interfaces,
                Classes: classes.AsReadOnly(),
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        private RawClassDto ParseClass(
            YamlMappingNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(
                node,
                typeof(RawClassDto),
                logicalPath,
                sourcePath).ToList();

            // --- ClassName (SimpleName only) ---
            var className = GetScalar(node, "className");
            if (string.IsNullOrWhiteSpace(className))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.ClassMissingName,
                    $"Missing 'className' in {logicalPath}.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.className")
                );
                className = "<missing.class>";
            }
            else if (!className.IsValidIdentifier())
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.InvalidIdentifier,
                    $"ClassName '{className}' must be a simple identifier (no namespace).",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.className")
                );
            }
            else if (className.Contains("."))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.ClassNameMustBeSimple,
                    $"ClassName '{className}' must not contain a namespace.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.className")
                );
            }

            // --- QualifiedInterfaceName (QualifiedName or null) ---
            var qInterface = GetScalar(node, "qualifiedInterfaceName");
            if (string.Equals(qInterface, "null", StringComparison.OrdinalIgnoreCase))
                qInterface = null;

            if (!string.IsNullOrWhiteSpace(qInterface))
            {
                if (!qInterface.TryParseTypeRef(
                    out var baseName,
                    out var isArray,
                    out var isContainerNullable,
                    out var isElementNullable))
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{qInterface}' at {logicalPath}.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")
                    );
                }
                else if (string.IsNullOrWhiteSpace(baseName) ||
                         !baseName.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.ClassInterfaceMustBeQualified,
                        $"QualifiedInterfaceName '{qInterface}' must include a namespace.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")
                    );
                }
            }

            // --- InitializerParameters ---
            var parameters = GetParameterTokens(
                node,
                "initializerParameters",
                sourcePath,
                $"{logicalPath}.initializerParameters");

            return new RawClassDto(
                ClassName: className,
                QualifiedInterfaceName: qInterface,
                InitializerParameters: parameters,
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }

        private RawParameterDto ParseParameter(
            YamlMappingNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = ValidateMappingKeys(
                node,
                typeof(RawParameterDto),
                logicalPath,
                sourcePath).ToList();

            // --- ParameterName ---
            var paramName = GetScalar(node, "parameterName");
            if (string.IsNullOrWhiteSpace(paramName))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.ParameterMissingName,
                    $"Missing 'parameterName' in {logicalPath}.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.parameterName")
                );
                paramName = "<missing.param>";
            }
            else if (!paramName.IsValidIdentifier())
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.InvalidIdentifier,
                    $"ParameterName '{paramName}' is not a valid identifier.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.parameterName")
                );
            }

            // --- QualifiedClassName / QualifiedInterfaceName ---
            var qClass = GetScalar(node, "qualifiedClassName");
            var qInterface = GetScalar(node, "qualifiedInterfaceName");

            if (string.Equals(qClass, "null", StringComparison.OrdinalIgnoreCase))
                qClass = null;
            if (string.Equals(qInterface, "null", StringComparison.OrdinalIgnoreCase))
                qInterface = null;

            if (!string.IsNullOrWhiteSpace(qClass) &&
                !string.IsNullOrWhiteSpace(qInterface))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.ParameterBothClassAndInterface,
                    $"Parameter at {logicalPath} specifies both qualifiedClassName and qualifiedInterfaceName. Exactly one must be non-null.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, logicalPath)
                );
            }
            else if (string.IsNullOrWhiteSpace(qClass) &&
                     string.IsNullOrWhiteSpace(qInterface))
            {
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.ParameterMissingClassOrInterface,
                    $"Parameter at {logicalPath} must specify either qualifiedClassName or qualifiedInterfaceName.",
                    provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                    fallbackLocation: BuildLocation(sourcePath, logicalPath)
                );
            }

            // --- Parse QualifiedClassName ---
            string? baseQClass = null;
            bool classIsArray = false, classIsContainerNullable = false,
                 classIsElementNullable = false;

            if (!string.IsNullOrWhiteSpace(qClass))
            {
                if (!qClass.TryParseTypeRef(
                    out baseQClass,
                    out classIsArray,
                    out classIsContainerNullable,
                    out classIsElementNullable))
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{qClass}' at {logicalPath}.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName")
                    );
                }
                else if (string.IsNullOrWhiteSpace(baseQClass) ||
                         !baseQClass.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.ParameterMissingQualifiedClass,
                        $"QualifiedClassName '{qClass}' must include a namespace.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedClassName")
                    );
                }
            }

            // --- Parse QualifiedInterfaceName ---
            string? baseQInterface = null;
            bool ifaceIsArray = false, ifaceIsContainerNullable = false,
                 ifaceIsElementNullable = false;

            if (!string.IsNullOrWhiteSpace(qInterface))
            {
                if (!qInterface.TryParseTypeRef(
                    out baseQInterface,
                    out ifaceIsArray,
                    out ifaceIsContainerNullable,
                    out ifaceIsElementNullable))
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.TypeRefInvalid,
                        $"Invalid type reference '{qInterface}' at {logicalPath}.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")
                    );
                }
                else if (string.IsNullOrWhiteSpace(baseQInterface) ||
                         !baseQInterface.IsQualifiedName())
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.ParameterMissingQualifiedInterface,
                        $"QualifiedInterfaceName '{qInterface}' must include a namespace.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.qualifiedInterfaceName")
                    );
                }
            }

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

        private RawInterfaceDto ParseInterface(
            YamlNode node,
            string sourcePath,
            string logicalPath)
        {
            var diagnostics = new List<Diagnostic>();
            string? name = null;

            if (node is YamlScalarNode scalar)
            {
                name = scalar.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.InterfaceMissingName,
                        $"Empty interface scalar at {logicalPath}.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, logicalPath)
                    );
                    name = "<missing.interface>";
                }
                else if (!name.IsValidIdentifier() || name.Contains("."))
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.InvalidIdentifier,
                        $"InterfaceName '{name}' must be a simple identifier.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, logicalPath)
                    );
                }
            }
            else if (node is YamlMappingNode map)
            {
                name = GetScalar(map, "interfaceName");
                if (string.IsNullOrWhiteSpace(name))
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.InterfaceMissingName,
                        $"Missing 'interfaceName' in {logicalPath}.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, $"{logicalPath}.interfaceName"),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.interfaceName")
                    );
                    name = "<missing.interface>";
                }
                else if (!name.IsValidIdentifier() || name.Contains("."))
                {
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.InvalidIdentifier,
                        $"InterfaceName '{name}' must be a simple identifier.",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, $"{logicalPath}.interfaceName"),
                        fallbackLocation: BuildLocation(sourcePath, $"{logicalPath}.interfaceName")
                    );
                }
            }
            else
            {
                var prov = MakeProvStack(node, sourcePath, logicalPath);
                DiagnosticsHelper.Add(
                    diagnostics,
                    DiagnosticCode.InterfaceMissingName,
                    $"Interface token at {logicalPath} must be a scalar or mapping node.",
                    provenance: prov,
                    fallbackLocation: BuildLocation(sourcePath, logicalPath)
                );
                name = "<invalid.interface>";
            }

            return new RawInterfaceDto(
                InterfaceName: name,
                ProvenanceStack: MakeProvStack(node, sourcePath, logicalPath),
                Diagnostics: diagnostics.AsReadOnly()
            );
        }
    }
}

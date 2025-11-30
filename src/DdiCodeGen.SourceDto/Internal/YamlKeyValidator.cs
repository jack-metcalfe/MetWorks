// src/DdiCodeGen.SourceDto/Internal/YamlKeyValidator.cs
namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using YamlDotNet.RepresentationModel;
    using DdiCodeGen.SourceDto.Raw;

    /// <summary>
    /// Validates YAML mapping node keys against a small, explicit allowed-key map.
    /// Emits NormalizationError entries with RawProvenanceEntry provenance for unknown or mis-cased keys.
    /// Intended to run immediately after parsing YAML and before deserialization into Raw DTOs.
    /// </summary>
    internal static class YamlKeyValidator
    {
        // Allowed keys per mapping context (case sensitive)
        // Extend this map if you add more contexts or nested objects.
        private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedKeysByContext =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["root"] = new HashSet<string>(StringComparer.Ordinal) { "CodeGen", "Assemblies", "NamedInstances", "Namespaces", "SourcePath", "Provenance" },
            ["CodeGen"] = new HashSet<string>(StringComparer.Ordinal) { "RegistryClass", "GeneratedCodePath", "ResourceProvider", "Namespace", "FailFast", "Enums", "Provenance" },
            ["Assemblies.item"] = new HashSet<string>(StringComparer.Ordinal) { "Assembly", "FullName", "Path", "Primitive", "Provenance" },
            ["Namespaces.item"] = new HashSet<string>(StringComparer.Ordinal) { "Namespace", "Types", "Interfaces", "Provenance" },
            ["Types.item"] = new HashSet<string>(StringComparer.Ordinal) { "Type", "FullName", "Assembly", "TypeKind", "Constructors", "Initializers", "Attributes", "ImplementedInterfaces", "Assignable", "Provenance" },
            ["NamedInstances.item"] = new HashSet<string>(StringComparer.Ordinal) { "NamedInstance", "Type", "AssignmentMode", "Initializer", "EagerLoad", "ExposeAsInterfaceName", "FailFast", "Assignments", "Elements", "Provenance" },
            // Add other contexts as needed
        };

        /// <summary>
        /// Validate YAML keys in the provided YAML text. Adds NormalizationError entries to <paramref name="errors"/>.
        /// </summary>
        /// <param name="yamlText">YAML document text.</param>
        /// <param name="sourcePath">Source path used in provenance entries.</param>
        /// <param name="errors">Accumulator for NormalizationError results.</param>
        public static void ValidateYamlKeys(string yamlText, string sourcePath, List<NormalizationError> errors)
        {
            if (string.IsNullOrEmpty(yamlText)) return;
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (string.IsNullOrWhiteSpace(sourcePath)) sourcePath = "<in-memory>";

            var yaml = new YamlStream();
            using var reader = new System.IO.StringReader(yamlText);
            yaml.Load(reader);

            if (yaml.Documents.Count == 0) return;

            if (!(yaml.Documents[0].RootNode is YamlMappingNode root)) return;

            ValidateMapping(root, "root", sourcePath, "<root>", errors);
        }

        private static void ValidateMapping(YamlMappingNode mapping, string contextKey, string sourcePath, string logicalPath, List<NormalizationError> errors)
        {
            AllowedKeysByContext.TryGetValue(contextKey, out var allowed);

            foreach (var kv in mapping.Children)
            {
                if (!(kv.Key is YamlScalarNode keyNode))
                {
                    // Non-scalar keys are unexpected; attach provenance if possible
                    var prov = MakeProvFromNode(kv.Key as YamlNode, sourcePath, logicalPath);
                    errors.Add(new NormalizationError($"Unknown or non-scalar key in context '{contextKey}'", prov));
                    continue;
                }

                var key = keyNode.Value ?? string.Empty;

                // If allowed set exists for this context, check membership
                if (allowed != null && !allowed.Contains(key))
                {
                    var prov = MakeProvFromNode(keyNode, sourcePath, logicalPath + "." + key);
                    errors.Add(new NormalizationError($"Unknown or forbidden key '{key}' in context '{contextKey}'", prov));
                }

                // Recurse into known collection or mapping contexts
                var childLogical = logicalPath + "." + key;
                var value = kv.Value;

                switch (key)
                {
                    case "Namespaces":
                    case "Assemblies":
                    case "NamedInstances":
                        if (value is YamlSequenceNode seq)
                        {
                            for (int i = 0; i < seq.Children.Count; i++)
                            {
                                if (seq.Children[i] is YamlMappingNode itemMap)
                                {
                                    var itemContext = key + ".item"; // e.g., "Namespaces.item"
                                    ValidateMapping(itemMap, itemContext, sourcePath, $"{childLogical}[{i}]", errors);
                                }
                            }
                        }
                        break;

                    case "Types":
                        if (value is YamlSequenceNode typesSeq)
                        {
                            for (int i = 0; i < typesSeq.Children.Count; i++)
                            {
                                if (typesSeq.Children[i] is YamlMappingNode typeMap)
                                {
                                    ValidateMapping(typeMap, "Types.item", sourcePath, $"{childLogical}[{i}]", errors);
                                }
                            }
                        }
                        break;

                    default:
                        // If value is a mapping, descend but use a composed context key
                        if (value is YamlMappingNode childMap)
                        {
                            var nextContext = contextKey + "." + key;
                            ValidateMapping(childMap, nextContext, sourcePath, childLogical, errors);
                        }
                        else if (value is YamlSequenceNode childSeq)
                        {
                            // For sequences of mappings, descend into each mapping
                            for (int i = 0; i < childSeq.Children.Count; i++)
                            {
                                if (childSeq.Children[i] is YamlMappingNode seqMap)
                                {
                                    var nextContext = contextKey + "." + key + ".item";
                                    ValidateMapping(seqMap, nextContext, sourcePath, $"{childLogical}[{i}]", errors);
                                }
                            }
                        }
                        break;
                }
            }
        }

        // Create a RawProvenanceEntry from a YamlNode's Start mark when available.
        private static RawProvenanceEntry? MakeProvFromNode(YamlNode? node, string sourcePath, string logicalPath)
        {
            if (node is null) return null;

            // YamlDotNet nodes expose Start/End marks via IYamlLineInfo on scalar nodes.
            // Use reflection-safe access: many node types expose Start property of type Mark.
            try
            {
                if (node is YamlScalarNode scalar)
                {
                    var mark = scalar.Start;
                    var origin = new RawProvenanceOrigin(sourcePath, mark.Line, mark.Column, logicalPath);
                    return new RawProvenanceEntry(origin, Stage: "parser", Tool: "yaml-key-validator", When: DateTimeOffset.UtcNow);
                }

                // For mapping/sequence nodes, try to use their Start property if present
                var startProp = node.GetType().GetProperty("Start");
                if (startProp != null)
                {
                    var markObj = startProp.GetValue(node);
                    if (markObj != null)
                    {
                        // Mark has Line and Column properties
                        var lineProp = markObj.GetType().GetProperty("Line");
                        var colProp = markObj.GetType().GetProperty("Column");
                        var line = lineProp?.GetValue(markObj) as int? ?? 0;
                        var col = colProp?.GetValue(markObj) as int?;
                        var origin = new RawProvenanceOrigin(sourcePath, line, col, logicalPath);
                        return new RawProvenanceEntry(origin, Stage: "parser", Tool: "yaml-key-validator", When: DateTimeOffset.UtcNow);
                    }
                }
            }
            catch
            {
                // ignore reflection failures and return a provenance entry with minimal info
            }

            // Fallback: return a provenance entry with no line/column
            var fallbackOrigin = new RawProvenanceOrigin(sourcePath, 0, 0, logicalPath);
            return new RawProvenanceEntry(fallbackOrigin, Stage: "parser", Tool: "yaml-key-validator", When: DateTimeOffset.UtcNow);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.RepresentationModel;
using DdiCodeGen.Dtos.Canonical;

namespace DdiCodeGen.Dtos.Internal
{
    internal sealed partial class Loader
    {
        // Safe scalar extraction helper
        private static string? GetScalar(YamlMappingNode map, string key)
        {
            if (map.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar)
                return scalar.Value;
            return null;
        }

        // Return null when child sequence is missing
        private static YamlSequenceNode? GetChildSequence(YamlMappingNode node, string key)
            => node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlSequenceNode seq ? seq : null;

        // Return null when child mapping is missing
        private static YamlMappingNode? GetChildMapping(YamlMappingNode node, string key)
            => node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlMappingNode map ? map : null;

        // Build a location string combining file path and logical path for diagnostics
        private static string BuildLocation(string? sourcePath, string logicalPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return logicalPath;
            return $"{sourcePath}#{logicalPath}";
        }

        // Add a diagnostic to a list (helper)
        private static void AddDiagnostic(List<Diagnostic> diagnostics, DiagnosticCode code, string message, string? location = null)
        {
            diagnostics.Add(new Diagnostic(code, message, location));
        }

        // Create a minimal provenance stack for a parsed node
        // Shared.cs
        // Primary implementation already returns nullable
        private static RawProvenanceStack? MakeProvStack(YamlNode node, string sourcePath, string logicalPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;

            var line = node.Start.Line;
            var col = node.Start.Column;
            if (line < 0 || col < 0)
                return null;

            var origin = new RawProvenanceOrigin(sourcePath, line, col, logicalPath);
            var entry = new RawProvenanceEntry(origin, "parser", "yaml-raw-loader", DateTimeOffset.UtcNow);
            return new RawProvenanceStack(Version: 1, Entries: new List<RawProvenanceEntry> { entry });
        }

        // Overload for mapping node provenance — now returns nullable to match the primary method
        private static RawProvenanceStack? MakeProvStack(YamlMappingNode node, string sourcePath, string logicalPath)
            => MakeProvStack((YamlNode)node, sourcePath, logicalPath);

        // Overload for scalar node provenance — now returns nullable to match the primary method
        private static RawProvenanceStack? MakeProvStack(YamlScalarNode node, string sourcePath, string logicalPath)
            => MakeProvStack((YamlNode)node, sourcePath, logicalPath);

        // Validate mapping keys against schema and return diagnostics
        private IReadOnlyList<Diagnostic> ValidateMappingKeys(YamlMappingNode map, Type dtoType, string logicalPath, string sourcePath)
        {
            var diagnostics = new List<Diagnostic>();

            if (!RawYamlSchema.AllowedKeys.TryGetValue(dtoType, out var allowed))
                return diagnostics;

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);

            foreach (var kv in map.Children)
            {
                if (kv.Key is not YamlScalarNode keyScalar) continue;
                var key = keyScalar.Value ?? string.Empty;
                if (!allowedSet.Contains(key))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCode.UnrecognizedToken,
                        $"Unrecognized token '{key}' at {logicalPath}. Allowed keys: {string.Join(", ", allowed)}",
                        
                        BuildLocation(sourcePath, logicalPath)
                    ));
                }
            }

            return diagnostics.ToList().AsReadOnly();
        }

        // Helper to parse interface tokens (scalar shorthand or mapping form)
        private IReadOnlyList<RawInterfaceDto> GetInterfaceTokens(YamlMappingNode node, string key, string sourcePath, string logicalPath)
        {
            var results = new List<RawInterfaceDto>();

            if (!node.Children.TryGetValue(new YamlScalarNode(key), out var value) || value is not YamlSequenceNode seq)
                return results;

            for (int i = 0; i < seq.Children.Count; i++)
            {
                var child = seq.Children[i];
                var childLogical = $"{logicalPath}[{i}]";

                if (child is YamlScalarNode scalar)
                {
                    var prov = MakeProvStack(scalar, sourcePath, childLogical);
                    var diags = new List<Diagnostic>();
                    if (string.IsNullOrWhiteSpace(scalar.Value))
                        diags.Add(new Diagnostic(DiagnosticCode.InterfaceMissingName, $"Empty interface scalar at {childLogical}.",  BuildLocation(sourcePath, childLogical)));
                    else if (!scalar.Value.IsValidIdentifier())
                        diags.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"Interface scalar '{scalar.Value}' is not a valid identifier.",  BuildLocation(sourcePath, childLogical)));

                    results.Add(new RawInterfaceDto(
                        InterfaceName: scalar.Value,
                        ProvenanceStack: prov,
                        Diagnostics: diags.ToList().AsReadOnly()
                    ));
                    continue;
                }

                if (child is YamlMappingNode map)
                {
                    var prov = MakeProvStack(map, sourcePath, childLogical);
                    var diags = ValidateMappingKeys(map, typeof(RawInterfaceDto), childLogical, sourcePath).ToList();
                    var name = GetScalar(map, "interfaceName");
                    if (string.IsNullOrWhiteSpace(name))
                        diags.Add(new Diagnostic(DiagnosticCode.InterfaceMissingName, $"Missing 'interfaceName' in {childLogical}.",  BuildLocation(sourcePath, $"{childLogical}.interfaceName")));
                    else if (!name.IsValidIdentifier())
                        diags.Add(new Diagnostic(DiagnosticCode.InvalidIdentifier, $"InterfaceName '{name}' is not a valid identifier.",  BuildLocation(sourcePath, $"{childLogical}.interfaceName")));

                    results.Add(new RawInterfaceDto(
                        InterfaceName: name,
                        ProvenanceStack: prov,
                        Diagnostics: diags.ToList().AsReadOnly()
                    ));
                    continue;
                }

                // Other node types are ignored; could add diagnostic if desired
            }

            return results.ToList().AsReadOnly();
        }
    }
}

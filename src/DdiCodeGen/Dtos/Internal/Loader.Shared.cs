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

        // Add a diagnostic to a list (helper) — now uses DiagnosticsHelper
        private static void AddDiagnostic(List<Diagnostic> diagnostics, DiagnosticCode code, string message, string? location = null)
        {
            // Use the provided location as fallback; provenance is not available here so pass null provenance
            DiagnosticsHelper.Add(diagnostics, code, message, fallbackLocation: location ?? "<unknown>");
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
                    // Use DiagnosticsHelper so location and provenance are consistent
                    DiagnosticsHelper.Add(
                        diagnostics,
                        DiagnosticCode.UnrecognizedToken,
                        $"Unrecognized token '{key}' at {logicalPath}. Allowed keys: {string.Join(", ", allowed)}",
                        provenance: ProvenanceHelper.MakeProvenance(sourcePath, logicalPath),
                        fallbackLocation: BuildLocation(sourcePath, logicalPath)
                    );
                }
            }

            return diagnostics.ToList().AsReadOnly();
        }

        // Helper to parse interface tokens (scalar shorthand or mapping form)
        private IReadOnlyList<RawInterfaceDto> GetInterfaceTokens(
            YamlMappingNode node,
            string key,
            string sourcePath,
            string logicalPath)
        {
            var results = new List<RawInterfaceDto>();

            if (!node.Children.TryGetValue(new YamlScalarNode(key), out var value) ||
                value is not YamlSequenceNode seq)
            {
                return results;
            }

            for (int i = 0; i < seq.Children.Count; i++)
            {
                var childLogical = $"{logicalPath}[{i}]";
                results.Add(ParseInterface(seq.Children[i], sourcePath, childLogical));
            }

            return results.AsReadOnly();
        }

        private IReadOnlyList<RawParameterDto> GetParameterTokens(
            YamlMappingNode node,
            string key,
            string sourcePath,
            string logicalPath)
        {
            var results = new List<RawParameterDto>();

            if (!node.Children.TryGetValue(new YamlScalarNode(key), out var value) ||
                value is not YamlSequenceNode seq)
            {
                return results;
            }

            for (int i = 0; i < seq.Children.Count; i++)
            {
                var childLogical = $"{logicalPath}[{i}]";

                if (seq.Children[i] is YamlMappingNode map)
                {
                    results.Add(ParseParameter(map, sourcePath, childLogical));
                }
                else
                {
                    var prov = MakeProvStack(seq.Children[i], sourcePath, childLogical);
                    var diags = new List<Diagnostic>();
                    DiagnosticsHelper.Add(
                        diags,
                        DiagnosticCode.ParameterInvalidNode,
                        $"Initializer parameter at {childLogical} must be a mapping node.",
                        provenance: prov,
                        fallbackLocation: BuildLocation(sourcePath, childLogical)
                    );

                    results.Add(new RawParameterDto(
                        ParameterName: "<invalid.param>",
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
                    ));
                }
            }

            return results.AsReadOnly();
        }

        private IReadOnlyList<RawNamedInstanceAssignmentDto> GetAssignmentTokens(
            YamlMappingNode node,
            string key,
            string sourcePath,
            string logicalPath)
        {
            var results = new List<RawNamedInstanceAssignmentDto>();

            if (!node.Children.TryGetValue(new YamlScalarNode(key), out var value) ||
                value is not YamlSequenceNode seq)
            {
                return results;
            }

            for (int i = 0; i < seq.Children.Count; i++)
            {
                var childLogical = $"{logicalPath}[{i}]";

                if (seq.Children[i] is YamlMappingNode map)
                {
                    results.Add(ParseAssignment(map, sourcePath, childLogical));
                }
                else
                {
                    var prov = MakeProvStack(seq.Children[i], sourcePath, childLogical);
                    var diags = new List<Diagnostic>();
                    DiagnosticsHelper.Add(
                        diags,
                        DiagnosticCode.AssignmentInvalidNode,
                        $"Assignment at {childLogical} must be a mapping node.",
                        provenance: prov,
                        fallbackLocation: BuildLocation(sourcePath, childLogical)
                    );

                    results.Add(new RawNamedInstanceAssignmentDto(
                        AssignmentParameterName: "<invalid.param>",
                        AssignmentValue: null,
                        AssignmentNamedInstanceName: null,
                        ProvenanceStack: prov,
                        Diagnostics: diags.AsReadOnly()
                    ));
                }
            }

            return results.AsReadOnly();
        }
    }
}

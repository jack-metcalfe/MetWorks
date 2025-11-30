// src/DdiCodeGen.SourceDto/Internal/ProvenanceNormalizer.cs
namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DdiCodeGen.SourceDto.Raw;
    using DdiCodeGen.SourceDto.Canonical;

    internal static class ProvenanceNormalizer
    {
        private const string InMemorySourceSentinel = "<in-memory>";
        private const int MinVersion = 1;

        // Normalize raw provenance into canonical ProvenanceStack.
        // Validation rules:
        //  - raw.Entries must be present and non-empty (error PROV001)
        //  - each entry must have an Origin with a non-empty LogicalPath (error PROV002 per entry)
        //  - Version must be >= 1; if missing default to 1 and emit a warning (PROV003)
        //  - Stage and Tool are defaulted when missing (Stage -> "parser", Tool -> toolId)
        public static NormalizationResult<ProvenanceStack> Normalize(RawProvenanceStack? raw, string toolId)
        {
            var errors = new List<NormalizationError>();

            if (raw?.Entries == null || raw.Entries.Count == 0)
            {
                errors.Add(new NormalizationError("PROV001: Missing provenance entries", raw?.Entries?.LastOrDefault()));
                return NormalizationResult<ProvenanceStack>.Fail(errors.ToArray());
            }

            // Validate version
            var version = raw.Version;
            if (version < MinVersion)
            {
                errors.Add(new NormalizationError($"PROV003: Invalid provenance Version '{version}'; must be >= {MinVersion}", raw.Entries.LastOrDefault()));
                // normalize to MinVersion for canonical output but still report error
                version = MinVersion;
            }

            var entries = new List<ProvenanceEntry>(raw.Entries.Count);

            for (var i = 0; i < raw.Entries.Count; i++)
            {
                var re = raw.Entries[i];
                var rentry = re ?? new RawProvenanceEntry(null, null, null, null);
                var origin = rentry.Origin ?? new RawProvenanceOrigin(null, null, null, null);

                // Build canonical origin values with safe defaults
                var logicalPath = string.IsNullOrWhiteSpace(origin.LogicalPath) ? string.Empty : origin.LogicalPath;
                var sourcePath = string.IsNullOrWhiteSpace(origin.SourcePath) ? InMemorySourceSentinel : origin.SourcePath!;
                var line = origin.LineZeroBased ?? 0;
                var column = origin.ColumnZeroBased;

                // Validate origin.logicalPath presence per-entry (strict)
                if (string.IsNullOrWhiteSpace(logicalPath))
                {
                    errors.Add(new NormalizationError($"PROV002: Provenance entry at index {i} missing logical path", re));
                }

                // Default stage/tool when missing
                var stage = string.IsNullOrWhiteSpace(rentry.Stage) ? "parser" : rentry.Stage!;
                var tool = string.IsNullOrWhiteSpace(rentry.Tool) ? toolId : rentry.Tool!;
                var when = rentry.When ?? DateTimeOffset.UtcNow;

                var canonicalOrigin = new ProvenanceOrigin(sourcePath, line, column, logicalPath);
                entries.Add(new ProvenanceEntry(canonicalOrigin, stage, tool, when));
            }

            // If any validation errors were found, return them all
            if (errors.Count > 0)
            {
                return NormalizationResult<ProvenanceStack>.Fail(errors.ToArray());
            }

            var stack = new ProvenanceStack(version, entries.ToArray());
            return NormalizationResult<ProvenanceStack>.Ok(stack);
        }

        public static string ToJson(ProvenanceStack stack) => System.Text.Json.JsonSerializer.Serialize(stack);
    }
}

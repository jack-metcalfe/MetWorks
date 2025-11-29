namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using DdiCodeGen.SourceDto.Raw;
    using DdiCodeGen.SourceDto.Canonical;

    internal static class ProvenanceNormalizer
    {
        private const string InMemorySourceSentinel = "<in-memory>";

        public static NormalizationResult<ProvenanceStack> Normalize(RawProvenanceStack? raw, string toolId)
        {
            if (raw?.Entries == null || raw.Entries.Count == 0)
            {
                var err = new NormalizationError("Missing provenance entries", null);
                return NormalizationResult<ProvenanceStack>.Fail(err);
            }

            var entries = raw.Entries.Select(re =>
            {
                var rentry = re ?? new RawProvenanceEntry(null, null, null, null);
                var origin = rentry.Origin ?? new RawProvenanceOrigin(null, null, null, null);

                var logicalPath = string.IsNullOrWhiteSpace(origin.LogicalPath) ? string.Empty : origin.LogicalPath;
                var sourcePath = string.IsNullOrWhiteSpace(origin.SourcePath) ? InMemorySourceSentinel : origin.SourcePath!;
                var line = origin.LineZeroBased ?? 0;
                var column = origin.ColumnZeroBased;

                var when = rentry.When ?? DateTimeOffset.UtcNow;
                var stage = string.IsNullOrWhiteSpace(rentry.Stage) ? "parser" : rentry.Stage!;
                var tool = string.IsNullOrWhiteSpace(rentry.Tool) ? toolId : rentry.Tool!;

                var canonicalOrigin = new ProvenanceOrigin(sourcePath, line, column, logicalPath);
                return new ProvenanceEntry(canonicalOrigin, stage, tool, when);
            }).ToArray();

            var latest = entries.Last();
            if (string.IsNullOrWhiteSpace(latest.Origin.LogicalPath))
            {
                var err = new NormalizationError("Provenance entry missing logical path", raw.Entries.LastOrDefault());
                return NormalizationResult<ProvenanceStack>.Fail(err);
            }

            var stack = new ProvenanceStack(1, entries);
            return NormalizationResult<ProvenanceStack>.Ok(stack);
        }

        public static string ToJson(ProvenanceStack stack) => JsonSerializer.Serialize(stack);
    }
}

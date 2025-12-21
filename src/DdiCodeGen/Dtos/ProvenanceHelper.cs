namespace DdiCodeGen.Dtos;

public static class ProvenanceHelper
{

    // --- Raw provenance helpers used by transformer ---
    public static string? BuildLocationFromRaw(RawProvenanceStack? rawProv)
    {
        if (rawProv?.Entries == null || rawProv.Entries.Count == 0) return null;
        var latest = rawProv.Entries[^1];
        return $"{latest.Origin.SourcePath}#{latest.Origin.LogicalPath}";
    }
    // Helper: create a RawProvenanceStack for a YAML node location
    public static RawProvenanceStack MakeRawProvenance(string? sourcePath, string logicalPath)
    {
        var origin = new RawProvenanceOrigin(sourcePath ?? "<in-memory>", 0, 0, logicalPath);
        var entry = new RawProvenanceEntry(origin, "yaml-parser", "YamlDtoLoader", DateTimeOffset.UtcNow);
        return new RawProvenanceStack(ProvenanceStack.MinVersion, new List<RawProvenanceEntry> { entry });
    }
    // Create a minimal provenance stack for a parsed node
    public static ProvenanceStack MakeProvenance(string? sourcePath, string logicalPath)
    {
        var origin = new ProvenanceOrigin(sourcePath ?? "<in-memory>", 0, null, logicalPath);
        var entry = new ProvenanceEntry(origin, "parser", "yaml-parser-v1", DateTimeOffset.UtcNow);
        return new ProvenanceStack(ProvenanceStack.MinVersion, new List<ProvenanceEntry> { entry });
    }
    public static string? BuildLocationFrom(RawProvenanceStack? rawProv)
    {
        if (rawProv?.Entries == null || rawProv.Entries.Count == 0) return null;
        var latest = rawProv.Entries[^1];
        return $"{latest.Origin.SourcePath}#{latest.Origin.LogicalPath}";
    }

    public static ProvenanceStack MakeProvenance(RawProvenanceStack? raw)
    {
        var entries = new List<ProvenanceEntry>();
        var version = raw?.Version ?? ProvenanceStack.MinVersion;

        if (raw?.Entries != null)
        {
            foreach (var rawEntry in raw.Entries)
            {
                if (rawEntry?.Origin == null) continue;

                var origin = new ProvenanceOrigin(
                    SourcePath: rawEntry.Origin.SourcePath ?? "<in-memory>",
                    LineZeroBased: rawEntry.Origin.LineZeroBased,
                    ColumnZeroBased: rawEntry.Origin.ColumnZeroBased,
                    LogicalPath: rawEntry.Origin.LogicalPath ?? "<missing>"
                );

                var entry = new ProvenanceEntry(
                    Origin: origin,
                    Stage: rawEntry.Stage ?? "<unknown-stage>",
                    Tool: rawEntry.Tool ?? "<unknown-tool>",
                    When: rawEntry.When
                );

                entries.Add(entry);
            }
        }

        return new ProvenanceStack(
            Version: version < ProvenanceStack.MinVersion ? ProvenanceStack.MinVersion : version,
            Entries: entries
        );
    }

    // EnsureProvenance: builds a minimal valid ProvenanceStack when needed
    public static ProvenanceStack MakeProvenance(
        ProvenanceStack? prov,
        string fallbackLocation)
    {
        if (prov is not null)
        {
            try
            {
                if (prov.Entries is not null && prov.Entries.Count > 0)
                    return prov;
            }
            catch
            {
                // fall through to create fallback
            }
        }

        var origin = new ProvenanceOrigin(
            SourcePath: fallbackLocation ?? "<unknown>",
            LineZeroBased: 0,
            ColumnZeroBased: null,
            LogicalPath: fallbackLocation ?? "<unknown>"
        );

        var entry = new ProvenanceEntry(
            Origin: origin,
            Stage: "transform",
            Tool: "ddi-transformer",
            When: DateTimeOffset.UtcNow
        );

        return new ProvenanceStack(
            Version: ProvenanceStack.MinVersion,
            Entries: new List<ProvenanceEntry> { entry }
        );
    }

}

namespace DdiCodeGen.Dtos.Canonical;

public sealed record ProvenanceOrigin(
    string SourcePath,        // "<in-memory>" when not from a file
    int LineZeroBased,        // 0 when not available
    int? ColumnZeroBased,     // optional
    string LogicalPath        // required, non-empty
);

public sealed record ProvenanceEntry(
    ProvenanceOrigin Origin,
    string Stage,             // "parser", "normalizer", "generator", etc.
    string Tool,              // e.g., "yaml-parser-v1"
    DateTimeOffset When       // UTC timestamp
);

public sealed record ProvenanceStack(
    int Version,
    IReadOnlyList<ProvenanceEntry> Entries
)
{
    public ProvenanceEntry Latest => Entries[^1];
    public const int MinVersion = 1;
}

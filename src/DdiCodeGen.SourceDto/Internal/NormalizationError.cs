namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using DdiCodeGen.SourceDto.Raw;

    public sealed class NormalizationError
    {
        public string Message { get; }
        public RawProvenanceEntry? ProvenanceEntry { get; }
        public Exception? Exception { get; }

        public NormalizationError(string message, RawProvenanceEntry? provenanceEntry = null, Exception? exception = null)
        {
            Message = message;
            ProvenanceEntry = provenanceEntry;
            Exception = exception;
        }

        public override string ToString()
        {
            var prov = ProvenanceEntry?.Origin?.LogicalPath ?? ProvenanceEntry?.Origin?.SourcePath ?? "<unknown>";
            return $"{Message} (provenance: {prov})";
        }
    }
}

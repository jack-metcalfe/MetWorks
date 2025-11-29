namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using System.Linq;
    using DdiCodeGen.SourceDto.Raw;
    using DdiCodeGen.SourceDto.Canonical;

    internal static class NamedInstanceNormalizer
    {
        public static NormalizationResult<NamedInstanceDto> Normalize(RawNamedInstanceDto raw, string toolId)
        {
            if (raw == null)
                return NormalizationResult<NamedInstanceDto>.Fail(new NormalizationError("RawNamedInstanceDto is null", null));

            if (string.IsNullOrWhiteSpace(raw.NamedInstance))
                return NormalizationResult<NamedInstanceDto>.Fail(new NormalizationError("NamedInstance key is missing", raw.Provenance?.Entries?.LastOrDefault()));

            var provResult = ProvenanceNormalizer.Normalize(raw.Provenance, toolId);
            if (!provResult.IsSuccess || provResult.Value is null)
                return NormalizationResult<NamedInstanceDto>.Fail(provResult.Errors!.ToArray());

            var assignments = (raw.Assignments ?? Array.Empty<RawNamedInstanceAssignmentDto>())
                .Select(a => new NamedInstanceAssignmentDto(
                    Assignment: a.Assignment ?? string.Empty,
                    Value: a.Value,
                    NamedInstance: a.NamedInstance,
                    ProvenanceStack: provResult.Value
                )).ToArray();

            var elements = (raw.Elements ?? Array.Empty<RawNamedInstanceElementDto>())
                .Select(e => new NamedInstanceElementDto(
                    Value: e.Value,
                    NamedInstance: e.NamedInstance,
                    ProvenanceStack: provResult.Value
                )).ToArray();

            var canonical = new NamedInstanceDto(
                NamedInstance: raw.NamedInstance,
                Type: raw.Type ?? string.Empty,
                AssignmentMode: raw.AssignmentMode ?? string.Empty,
                Initializer: raw.Initializer,
                EagerLoad: raw.EagerLoad ?? false,
                ExposeAsInterface: raw.ExposeAsInterface,
                FailFast: raw.FailFast ?? false,
                Assignments: assignments,
                Elements: elements,
                ProvenanceStack: provResult.Value
            );

            return NormalizationResult<NamedInstanceDto>.Ok(canonical);
        }
    }
}

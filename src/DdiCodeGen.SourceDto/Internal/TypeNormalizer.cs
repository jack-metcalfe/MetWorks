namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using System.Linq;
    using DdiCodeGen.SourceDto.Raw;
    using DdiCodeGen.SourceDto.Canonical;

    internal static class TypeNormalizer
    {
        public static NormalizationResult<TypeDto> Normalize(RawTypeDto raw, string toolId)
        {
            if (raw == null)
                return NormalizationResult<TypeDto>.Fail(new NormalizationError("RawTypeDto is null", null));

            if (string.IsNullOrWhiteSpace(raw.Type))
                return NormalizationResult<TypeDto>.Fail(new NormalizationError("Type key is missing", raw.ProvenanceStack?.Entries?.LastOrDefault()));

            var provResult = ProvenanceNormalizer.Normalize(raw.ProvenanceStack, toolId);
            if (!provResult.IsSuccess || provResult.Value is null)
                return NormalizationResult<TypeDto>.Fail(provResult.Errors!.ToArray());

            var initializers = (raw.Initializers ?? Array.Empty<RawInitializerDto>())
                .Select(ri =>
                {
                    var parameters = (ri.Parameters ?? Array.Empty<RawParameterDto>())
                        .Select(p => new ParameterDto(
                            Parameter: p.Parameter ?? string.Empty,
                            Type: p.Type,
                            Interface: p.Interface,
                            ProvenanceStack: provResult.Value
                        )).ToArray();

                    return new InitializerDto(
                        Initializer: ri.Initializer ?? string.Empty,
                        Eager: ri.Eager ?? false,
                        Order: ri.Order ?? 0,
                        Parameters: parameters,
                        ProvenanceStack: provResult.Value
                    );
                }).ToArray();

            var canonical = new TypeDto(
                Type: raw.Type,
                Assignable: raw.Assignable ?? false,
                FullName: raw.FullName ?? string.Empty,
                Assembly: raw.Assembly ?? string.Empty,
                TypeKind: raw.TypeKind ?? string.Empty,
                Initializers: initializers,
                Attributes: raw.Attributes ?? Array.Empty<string>(),
                ImplementedInterfaces: raw.ImplementedInterfaces ?? Array.Empty<string>(),
                ProvenanceStack: provResult.Value
            );

            return NormalizationResult<TypeDto>.Ok(canonical);
        }
    }
}

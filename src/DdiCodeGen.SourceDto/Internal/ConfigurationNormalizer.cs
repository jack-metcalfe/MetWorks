// src/DdiCodeGen.SourceDto/Internal/ConfigurationNormalizer.cs
// Normalizer that converts Raw DTOs into Canonical DTOs and enforces canonical invariants.
// Uses explicit null / IsSuccess checks for provenance normalization so the compiler
// and static analysis are satisfied without blind null-forgiveness.
namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DdiCodeGen.SourceDto.Raw;
    using DdiCodeGen.SourceDto.Canonical;

    /// <summary>
    /// Converts raw parsed DTOs into canonical, validated DTOs.
    /// Produces provenance-anchored errors via NormalizationResult/NormalizationError.
    /// </summary>
    internal static class ConfigurationNormalizer
    {
        private const string InMemorySourcePath = "<in-memory>";
        private const string ToolId = "ddi-normalizer-v1";

        public static NormalizationResult<ConfigurationDto> Normalize(RawConfigurationDto raw)
        {
            if (raw == null)
            {
                return NormalizationResult<ConfigurationDto>.Fail(
                    new NormalizationError("Raw configuration is null", null));
            }

            var errors = new List<NormalizationError>();

            // Top-level provenance for the configuration
            var configProvResult = ProvenanceNormalizer.Normalize(raw.Provenance, ToolId);

            // If top-level provenance normalization fails, propagate its errors and abort.
            if (configProvResult is null || configProvResult.Value is null || !configProvResult.IsSuccess )
            {
                if (configProvResult?.Errors != null) errors.AddRange(configProvResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize top-level provenance", raw.Provenance?.Entries?.LastOrDefault()));
                return NormalizationResult<ConfigurationDto>.Fail(errors.First());
            }

            var configProv = configProvResult.Value; // safe: IsSuccess checked above

            // SourcePath sentinel
            var sourcePath = string.IsNullOrWhiteSpace(raw.SourcePath) ? InMemorySourcePath : raw.SourcePath;

            // CodeGen is required in canonical model
            if (raw.CodeGen == null)
            {
                errors.Add(new NormalizationError(
                    "Missing required 'codeGen' section",
                    raw.Provenance?.Entries?.LastOrDefault()));
                return NormalizationResult<ConfigurationDto>.Fail(errors.First());
            }

            // Normalize assemblies
            var assemblies = (raw.Assemblies ?? Array.Empty<RawAssemblyDto>())
                .Select(a => NormalizeAssembly(a, configProv, errors))
                .ToArray();

            // Normalize namespaces (types and interfaces)
            var namespaces = (raw.Namespaces ?? Array.Empty<RawNamespaceDto>())
                .Select(ns => NormalizeNamespace(ns, configProv, errors))
                .ToArray();

            // Normalize named instances
            var namedInstances = (raw.NamedInstances ?? Array.Empty<RawNamedInstanceDto>())
                .Select(ri => NormalizeNamedInstance(ri, configProv, errors))
                .ToArray();

            // Normalize CodeGen (now required)
            var codeGen = NormalizeCodeGen(raw.CodeGen, configProv, errors);

            // If any normalization errors so far, return aggregated failure
            if (errors.Count > 0)
            {
                return NormalizationResult<ConfigurationDto>.Fail(errors.First());
            }

            // Post-normalization validation: enforce ExposeAsInterface resolves to interface and type implements it
            var canonicalNamespaces = namespaces; // alias
            var validatedNamedInstances = new List<NamedInstanceDto>();
            foreach (var rawNi in raw.NamedInstances ?? Array.Empty<RawNamedInstanceDto>())
            {
                // find canonical counterpart by key
                var canonicalNi = namedInstances.FirstOrDefault(n => n.NamedInstance == (rawNi.NamedInstance ?? string.Empty));
                if (canonicalNi == null)
                {
                    // This should not happen because we normalized them above, but guard defensively
                    errors.Add(new NormalizationError(
                        $"Internal error: canonical named instance for '{rawNi.NamedInstance}' not found",
                        rawNi.Provenance?.Entries?.LastOrDefault()));
                    continue;
                }

                var validation = ExposureValidator.ValidateExposeAs(rawNi, canonicalNi, canonicalNamespaces, ToolId);
                if (!validation.IsSuccess)
                {
                    if (validation.Errors != null) errors.AddRange(validation.Errors);
                }
                else
                {
                    validatedNamedInstances.Add(validation.Value!);
                }
            }

            if (errors.Count > 0)
            {
                return NormalizationResult<ConfigurationDto>.Fail(errors.First());
            }

            // Build final canonical configuration
            var canonicalConfig = new ConfigurationDto(
                CodeGen: codeGen,
                Assemblies: assemblies,
                NamedInstances: validatedNamedInstances,
                Namespaces: namespaces,
                SourcePath: sourcePath,
                ProvenanceStack: configProv
            );

            return NormalizationResult<ConfigurationDto>.Ok(canonicalConfig);
        }

        private static CodeGenDto NormalizeCodeGen(RawCodeGenDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var cgProvResult = ProvenanceNormalizer.Normalize(raw?.Provenance, ToolId);

            // If provenance normalization failed, propagate errors and use parentProv as fallback
            ProvenanceStack cgProv;
            if (cgProvResult is null || cgProvResult.Value is null || !cgProvResult.IsSuccess)
            {
                if (cgProvResult?.Errors != null) errors.AddRange(cgProvResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize codeGen provenance", raw?.Provenance?.Entries?.LastOrDefault()));
                cgProv = parentProv;
            }
            else
            {
                cgProv = cgProvResult.Value;
            }

            // Required fields: RegistryClass, GeneratedCodePath, ResourceProvider, Namespace
            if (string.IsNullOrWhiteSpace(raw?.RegistryClass))
            {
                errors.Add(new NormalizationError("codeGen.registryClass is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.GeneratedCodePath))
            {
                errors.Add(new NormalizationError("codeGen.generatedCodePath is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.ResourceProvider))
            {
                errors.Add(new NormalizationError("codeGen.resourceProvider is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.Namespace))
            {
                errors.Add(new NormalizationError("codeGen.namespace is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }

            var enums = (raw?.Enums ?? Array.Empty<RawCodeGenEnumsDto>())
                .Select(e => new CodeGenEnumsDto(
                    EnumName: e.EnumName ?? string.Empty,
                    Scope: e.Scope ?? string.Empty,
                    ProvenanceStack: cgProv))
                .ToArray();

            // If CodeGenDto requires a NamedInstanceAccessorDto parameter, supply null for now.
            NamedInstanceAccessorDto? namedInstanceAccessor = null;

            return new CodeGenDto(
                RegistryClass: raw!.RegistryClass ?? string.Empty,
                GeneratedCodePath: raw.GeneratedCodePath ?? string.Empty,
                ResourceProvider: raw.ResourceProvider ?? string.Empty,
                Namespace: raw.Namespace ?? string.Empty,
                FailFast: raw.FailFast ?? false,
                Enums: enums,
                NamedInstanceAccessor: namedInstanceAccessor,
                ProvenanceStack: cgProv
            );
        }

        private static AssemblyDto NormalizeAssembly(RawAssemblyDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var provResult = ProvenanceNormalizer.Normalize(raw?.Provenance, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize assembly provenance", raw?.Provenance?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Assembly))
            {
                errors.Add(new NormalizationError("assembly.assembly is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }

            return new AssemblyDto(
                Assembly: raw?.Assembly ?? string.Empty,
                FullName: raw?.FullName ?? string.Empty,
                Path: raw?.Path ?? string.Empty,
                Primitive: raw?.Primitive ?? false,
                ProvenanceStack: prov
            );
        }

        private static NamespaceDto NormalizeNamespace(RawNamespaceDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var provResult = ProvenanceNormalizer.Normalize(raw?.Provenance, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize namespace provenance", raw?.Provenance?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Namespace))
            {
                errors.Add(new NormalizationError("namespace.key is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }

            var types = (raw?.Types ?? Array.Empty<RawTypeDto>())
                .Select(t => NormalizeType(t, prov, errors))
                .ToArray();

            var interfaces = (raw?.Interfaces ?? Array.Empty<RawInterfaceDto>())
                .Select(i => new InterfaceDto(
                    Interface: i.Interface ?? string.Empty,
                    Assembly: i.Assembly ?? string.Empty,
                    ProvenanceStack: prov))
                .ToArray();

            return new NamespaceDto(
                Namespace: raw?.Namespace ?? string.Empty,
                Types: types,
                Interfaces: interfaces,
                ProvenanceStack: prov
            );
        }

        private static TypeDto NormalizeType(RawTypeDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var provResult = ProvenanceNormalizer.Normalize(raw?.Provenance, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize type provenance", raw?.Provenance?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Type))
            {
                errors.Add(new NormalizationError("type.Type is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }

            var genericParameterNames = raw?.GenericParameterNames ?? Array.Empty<string>();
            var initializers = (raw?.Initializers ?? Array.Empty<RawInitializerDto>())
                .Select(i => NormalizeInitializer(i, prov, errors))
                .ToArray();

            return new TypeDto(
                Type: raw?.Type ?? string.Empty,
                FullName: raw?.FullName ?? string.Empty,
                Assembly: raw?.Assembly ?? string.Empty,
                TypeKind: raw?.TypeKind ?? "Unknown",
                GenericArity: raw?.GenericArity ?? 0,
                GenericParameterNames: genericParameterNames,
                Initializers: initializers,
                Attributes: raw?.Attributes ?? Array.Empty<string>(),
                ImplementedInterfaces: raw?.ImplementedInterfaces ?? Array.Empty<string>(),
                Assignable: raw?.Assignable ?? false,
                ProvenanceStack: prov
            );
        }

        private static InitializerDto NormalizeInitializer(RawInitializerDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var provResult = ProvenanceNormalizer.Normalize(raw?.Provenance, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize initializer provenance", raw?.Provenance?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Initializer))
            {
                errors.Add(new NormalizationError("initializer.Initializer is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }

            var parameters = (raw?.Parameters ?? Array.Empty<RawParameterDto>())
                .Select(p => NormalizeParameter(p, prov, errors))
                .ToArray();

            return new InitializerDto(
                Initializer: raw?.Initializer ?? string.Empty,
                Eager: raw?.Eager ?? false,
                Order: raw?.Order ?? 0,
                Parameters: parameters,
                ProvenanceStack: prov
            );
        }

        private static ParameterDto NormalizeParameter(RawParameterDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var provResult = ProvenanceNormalizer.Normalize(raw?.Provenance, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize parameter provenance", raw?.Provenance?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Parameter))
            {
                errors.Add(new NormalizationError("parameter.Parameter is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }

            return new ParameterDto(
                Parameter: raw?.Parameter ?? string.Empty,
                Type: raw?.Type,
                Interface: raw?.Interface,
                ProvenanceStack: prov
            );
        }

        private static NamedInstanceDto NormalizeNamedInstance(RawNamedInstanceDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var provResult = ProvenanceNormalizer.Normalize(raw?.Provenance, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize namedInstance provenance", raw?.Provenance?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.NamedInstance))
            {
                errors.Add(new NormalizationError("namedInstance.key is required", raw?.Provenance?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.Type))
            {
                errors.Add(new NormalizationError($"namedInstance '{raw?.NamedInstance}' missing typeKey", raw?.Provenance?.Entries?.LastOrDefault()));
            }

            var assignments = (raw?.Assignments ?? Array.Empty<RawNamedInstanceAssignmentDto>())
                .Select(a => new NamedInstanceAssignmentDto(
                    Assignment: a.Assignment ?? string.Empty,
                    Value: a.Value,
                    NamedInstance: a.NamedInstance,
                    ProvenanceStack: prov))
                .ToArray();

            var elements = (raw?.Elements ?? Array.Empty<RawNamedInstanceElementDto>())
                .Select(e => new NamedInstanceElementDto(
                    Value: e.Value ?? string.Empty,
                    NamedInstance: e.NamedInstance,
                    ProvenanceStack: prov))
                .ToArray();

            return new NamedInstanceDto(
                NamedInstance: raw?.NamedInstance ?? string.Empty,
                Type: raw?.Type ?? string.Empty,
                AssignmentMode: raw?.AssignmentMode ?? string.Empty,
                Initializer: raw?.Initializer,
                EagerLoad: raw?.EagerLoad ?? false,
                ExposeAsInterface: raw?.ExposeAsInterface,
                FailFast: raw?.FailFast ?? false,
                Assignments: assignments,
                Elements: elements,
                ProvenanceStack: prov
            );
        }
    }
}

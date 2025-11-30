// src/DdiCodeGen.SourceDto/Internal/ConfigurationNormalizer.cs
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
            if (raw is null)
            {
                return NormalizationResult<ConfigurationDto>.Fail(
                    new NormalizationError("Raw configuration is null", null));
            }

            var errors = new List<NormalizationError>();

            // Top-level provenance for the configuration
            var configProvResult = ProvenanceNormalizer.Normalize(raw.ProvenanceStack, ToolId);

            // If top-level provenance normalization fails, propagate its errors and abort.
            if (configProvResult is null || configProvResult.Value is null || !configProvResult.IsSuccess)
            {
                if (configProvResult?.Errors != null) errors.AddRange(configProvResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize top-level provenance", raw.ProvenanceStack?.Entries?.LastOrDefault()));
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
                    raw.ProvenanceStack?.Entries?.LastOrDefault()));
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
                        rawNi.ProvenanceStack?.Entries?.LastOrDefault()));
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
                return NormalizationResult<ConfigurationDto>.Fail(errors.ToArray());
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
            var cgProvResult = ProvenanceNormalizer.Normalize(raw?.ProvenanceStack, ToolId);

            // If provenance normalization failed, propagate errors and use parentProv as fallback
            ProvenanceStack cgProv;
            if (cgProvResult is null || cgProvResult.Value is null || !cgProvResult.IsSuccess)
            {
                if (cgProvResult?.Errors != null) errors.AddRange(cgProvResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize codeGen provenance", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
                cgProv = parentProv;
            }
            else
            {
                cgProv = cgProvResult.Value;
            }

            // Required fields: RegistryClass, GeneratedCodePath, ResourceProvider, Namespace
            if (string.IsNullOrWhiteSpace(raw?.RegistryClass))
            {
                errors.Add(new NormalizationError("codeGen.registryClass is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.GeneratedCodePath))
            {
                errors.Add(new NormalizationError("codeGen.generatedCodePath is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.ResourceProvider))
            {
                errors.Add(new NormalizationError("codeGen.resourceProvider is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.Namespace))
            {
                errors.Add(new NormalizationError("codeGen.namespace is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
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
            var provResult = ProvenanceNormalizer.Normalize(raw?.ProvenanceStack, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize assembly provenance", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Assembly))
            {
                errors.Add(new NormalizationError("assembly.assembly is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
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
            var provResult = ProvenanceNormalizer.Normalize(raw?.ProvenanceStack, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize namespace provenance", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Namespace))
            {
                errors.Add(new NormalizationError("namespace.key is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
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

        // TODO(dd): temporary made public to allow tests; revert to internal and add strong-name signing + InternalsVisibleTo with public key before release
        public static TypeDto NormalizeType(RawTypeDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            if (raw is null)
            {
                errors.Add(new NormalizationError("RawTypeDto is null", null));
                return new TypeDto(
                    Type: string.Empty,
                    FullName: string.Empty,
                    Assembly: string.Empty,
                    TypeKind: "Unknown",
                    Initializers: Array.Empty<InitializerDto>(),
                    Attributes: Array.Empty<string>(),
                    ImplementedInterfaces: Array.Empty<string>(),
                    Assignable: false,
                    ProvenanceStack: parentProv);
            }

            var provResult = ProvenanceNormalizer.Normalize(raw.ProvenanceStack, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize type provenance", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            // Strict checks: no generics, no assembly tokens in Type, required Type present
            // Perform explicit checks here so messages are consistent and provenance is attached.
            var typeProv = raw?.ProvenanceStack?.Entries?.LastOrDefault();

            if (string.IsNullOrWhiteSpace(raw?.Type))
            {
                errors.Add(new NormalizationError("type.Type is required", typeProv));
            }
            else
            {
                var typeStr = raw.Type!;

                // Backtick generic notation (CLR generic definition)
                if (typeStr.IndexOf('`') >= 0)
                {
                    errors.Add(new NormalizationError(
                        $"Type '{typeStr}' appears to use backtick generic notation; generics are disallowed in input.",
                        typeProv));
                }

                // Angle-bracket generic syntax (C# style)
                if (typeStr.Contains('<') && typeStr.Contains('>'))
                {
                    errors.Add(new NormalizationError(
                        $"Type '{typeStr}' appears to use angle-bracket generic syntax; generics are disallowed in input.",
                        typeProv));
                }

                // Assembly-qualified names (comma indicates assembly qualifiers)
                if (typeStr.Contains(',', StringComparison.Ordinal))
                {
                    errors.Add(new NormalizationError(
                        $"Type '{typeStr}' contains a comma; assembly qualifiers are not allowed in Type. Use FullName for assembly full names.",
                        typeProv));
                }

                // Trailing dot (empty simple name)
                if (typeStr.EndsWith(".", StringComparison.Ordinal))
                {
                    errors.Add(new NormalizationError(
                        $"Type '{typeStr}' ends with a dot; simple type name is empty.",
                        typeProv));
                }
            }

            var initializers = (raw?.Initializers ?? Array.Empty<RawInitializerDto>())
                .Select(i => NormalizeInitializer(i, prov, errors))
                .ToArray();

            return new TypeDto(
                Type: raw?.Type ?? string.Empty,
                FullName: raw?.FullName ?? string.Empty,
                Assembly: raw?.Assembly ?? string.Empty,
                TypeKind: raw?.TypeKind ?? "Unknown",
                Initializers: initializers,
                Attributes: raw?.Attributes ?? Array.Empty<string>(),
                ImplementedInterfaces: raw?.ImplementedInterfaces ?? Array.Empty<string>(),
                Assignable: raw?.Assignable ?? false,
                ProvenanceStack: prov
            );
        }

        private static InitializerDto NormalizeInitializer(RawInitializerDto raw, ProvenanceStack parentProv, List<NormalizationError> errors)
        {
            var provResult = ProvenanceNormalizer.Normalize(raw?.ProvenanceStack, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize initializer provenance", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Initializer))
            {
                errors.Add(new NormalizationError("initializer.Initializer is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
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
            var provResult = ProvenanceNormalizer.Normalize(raw?.ProvenanceStack, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize parameter provenance", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.Parameter))
            {
                errors.Add(new NormalizationError("parameter.Parameter is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
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
            var provResult = ProvenanceNormalizer.Normalize(raw?.ProvenanceStack, ToolId);

            ProvenanceStack prov;
            if (provResult is null || provResult.Value is null || !provResult.IsSuccess)
            {
                if (provResult?.Errors != null) errors.AddRange(provResult.Errors);
                else errors.Add(new NormalizationError("Failed to normalize namedInstance provenance", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
                prov = parentProv;
            }
            else
            {
                prov = provResult.Value;
            }

            if (string.IsNullOrWhiteSpace(raw?.NamedInstance))
            {
                errors.Add(new NormalizationError("namedInstance.key is required", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
            }
            if (string.IsNullOrWhiteSpace(raw?.Type))
            {
                errors.Add(new NormalizationError($"namedInstance '{raw?.NamedInstance}' missing typeKey", raw?.ProvenanceStack?.Entries?.LastOrDefault()));
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
                ExposeAsInterfaceName: raw?.ExposeAsInterfaceName,
                FailFast: raw?.FailFast ?? false,
                Assignments: assignments,
                Elements: elements,
                ProvenanceStack: prov
            );
        }

        // -------------------------
        // No-generics validation (defensive)
        // -------------------------
        private static void ValidateNoGenericsInType(RawTypeDto raw, List<NormalizationError> errors)
        {
            var provEntry = MapBestProvenanceEntry(raw.ProvenanceStack);

            if (string.IsNullOrWhiteSpace(raw?.Type)) return;

            var type = raw.Type!;

            // Backtick generic notation
            if (type.IndexOf('`') >= 0)
            {
                errors.Add(new NormalizationError(
                    message: $"Type '{type}' appears to use backtick generic notation; generics are disallowed in input.",
                    provenanceEntry: provEntry));
            }

            // Angle-bracket generic notation
            if (type.IndexOf('<') >= 0 || type.IndexOf('>') >= 0)
            {
                errors.Add(new NormalizationError(
                    message: $"Type '{type}' appears to use angle-bracket generic notation; generics are disallowed in input.",
                    provenanceEntry: provEntry));
            }

            // Assembly-qualified tokens inside Type
            if (type.Contains("Version=", StringComparison.Ordinal)
                || type.Contains("Culture=", StringComparison.Ordinal)
                || type.Contains("PublicKeyToken=", StringComparison.Ordinal))
            {
                errors.Add(new NormalizationError(
                    message: $"Type '{type}' contains assembly qualifiers; use FullName for assembly full names and Type for plain type names.",
                    provenanceEntry: provEntry));
            }

            // Nested type '+' notation (disallowed)
            if (type.Contains('+'))
            {
                errors.Add(new NormalizationError(
                    message: $"Type '{type}' appears to be a nested type using '+' notation; nested types are not allowed in input.",
                    provenanceEntry: provEntry));
            }
        }

        // -------------------------
        // Provenance helpers
        // -------------------------
        private static RawProvenanceEntry? MapBestProvenanceEntry(RawProvenanceStack? rawStack)
        {
            if (rawStack?.Entries is null || rawStack.Entries.Count == 0) return null;
            // prefer last entry, fallback to first
            return rawStack.Entries[^1] ?? rawStack.Entries[0];
        }

        private static ProvenanceOrigin? MapBestProvenanceOrigin(RawProvenanceStack? rawStack)
        {
            var entry = MapBestProvenanceEntry(rawStack);
            if (entry?.Origin is null) return null;

            var o = entry.Origin;
            return new ProvenanceOrigin(
                SourcePath: o.SourcePath ?? InMemorySourcePath,
                LineZeroBased: o.LineZeroBased ?? 0,
                ColumnZeroBased: o.ColumnZeroBased,
                LogicalPath: o.LogicalPath ?? string.Empty
            );
        }

        private static ProvenanceStack MapProvenanceStack(RawProvenanceStack? rawStack)
        {
            if (rawStack is null)
            {
                return new ProvenanceStack(Version: 1, Entries: Array.Empty<ProvenanceEntry>());
            }

            var entries = (rawStack.Entries ?? Array.Empty<RawProvenanceEntry>())
                .Select(e =>
                {
                    if (e is null)
                    {
                        return new ProvenanceEntry(
                            Origin: new ProvenanceOrigin(InMemorySourcePath, 0, null, string.Empty),
                            Stage: string.Empty,
                            Tool: string.Empty,
                            When: DateTimeOffset.UtcNow);
                    }

                    var origin = e.Origin is null
                        ? new ProvenanceOrigin(InMemorySourcePath, 0, null, string.Empty)
                        : new ProvenanceOrigin(
                            SourcePath: e.Origin.SourcePath ?? InMemorySourcePath,
                            LineZeroBased: e.Origin.LineZeroBased ?? 0,
                            ColumnZeroBased: e.Origin.ColumnZeroBased,
                            LogicalPath: e.Origin.LogicalPath ?? string.Empty);

                    return new ProvenanceEntry(
                        Origin: origin,
                        Stage: e.Stage ?? string.Empty,
                        Tool: e.Tool ?? string.Empty,
                        When: e.When ?? DateTimeOffset.UtcNow);
                })
                .ToArray();

            return new ProvenanceStack(rawStack.Version, entries);
        }

        // -------------------------
        // Utility
        // -------------------------
        private static bool ShouldFailFast(RawTypeDto raw)
        {
            // Default: do not fail fast at type-level. Global FailFast is handled at CodeGen or orchestration level.
            return false;
        }
    }
}

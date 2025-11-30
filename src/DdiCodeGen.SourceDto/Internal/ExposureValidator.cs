namespace DdiCodeGen.SourceDto.Internal
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using DdiCodeGen.SourceDto.Raw;
    using DdiCodeGen.SourceDto.Canonical;

    /// <summary>
    /// Validates that a NamedInstance's ExposeAsInterface value resolves to an interface
    /// and that the concrete TypeKey implements that interface.
    /// </summary>
    internal static class ExposureValidator
    {
        private static InterfaceDto? ResolveInterface(string interfaceKey, IReadOnlyList<NamespaceDto> namespaces)
        {
            if (string.IsNullOrWhiteSpace(interfaceKey)) return null;

            return namespaces
                .SelectMany(ns => ns.Interfaces)
                .FirstOrDefault(i => string.Equals(i.Interface, interfaceKey, StringComparison.Ordinal));
        }

        private static bool ConcreteTypeImplementsInterface(string concreteTypeKey, InterfaceDto interfaceDto, IReadOnlyList<NamespaceDto> namespaces)
        {
            if (string.IsNullOrWhiteSpace(concreteTypeKey) || interfaceDto == null) return false;

            var type = namespaces
                .SelectMany(ns => ns.Types)
                .FirstOrDefault(t => string.Equals(t.Type, concreteTypeKey, StringComparison.Ordinal));

            if (type == null) return false;

            return type.ImplementedInterfaces.Any(i => string.Equals(i, interfaceDto.Interface, StringComparison.Ordinal));
        }

        public static NormalizationResult<NamedInstanceDto> ValidateExposeAs(
            RawNamedInstanceDto raw,
            NamedInstanceDto canonical,
            IReadOnlyList<NamespaceDto> canonicalNamespaces,
            string toolId)
        {
            if (canonical == null)
                return NormalizationResult<NamedInstanceDto>.Fail(new NormalizationError("Canonical NamedInstanceDto is null", raw?.ProvenanceStack?.Entries?.LastOrDefault()));

            if (string.IsNullOrWhiteSpace(canonical.ExposeAsInterfaceName))
                return NormalizationResult<NamedInstanceDto>.Ok(canonical);

            var iface = ResolveInterface(canonical.ExposeAsInterfaceName, canonicalNamespaces);
            if (iface == null)
            {
                var err = new NormalizationError(
                    $"ExposeAsInterface '{canonical.ExposeAsInterfaceName}' does not resolve to a known interface",
                    raw?.ProvenanceStack?.Entries?.LastOrDefault());
                return NormalizationResult<NamedInstanceDto>.Fail(err);
            }

            if (!ConcreteTypeImplementsInterface(canonical.Type, iface, canonicalNamespaces))
            {
                var err = new NormalizationError(
                    $"Type '{canonical.Type}' does not implement interface '{canonical.ExposeAsInterfaceName}'",
                    raw?.ProvenanceStack?.Entries?.LastOrDefault());
                return NormalizationResult<NamedInstanceDto>.Fail(err);
            }

            return NormalizationResult<NamedInstanceDto>.Ok(canonical);
        }
    }
}

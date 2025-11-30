using System;
using System.Linq;
using Xunit;
using DdiCodeGen.SourceDto.Internal;
using DdiCodeGen.SourceDto.Raw;
using DdiCodeGen.SourceDto.Canonical;
using System.Diagnostics;

namespace DdiCodeGen.SourceDto.Tests
{
    public class TypeNormalizationTests
    {
        private static RawProvenanceStack MakeProv(string sourcePath = "file.yaml", int line = 1, int col = 0, string logical = "Namespaces[0].Types[0]")
        {
            var origin = new RawProvenanceOrigin(sourcePath, line, col, logical);
            var entry = new RawProvenanceEntry(origin, Stage: "test", Tool: "unit-test", When: DateTimeOffset.UtcNow);
            return new RawProvenanceStack(Version: 1, Entries: new[] { entry });
        }

        private static ProvenanceStack EmptyProvenanceStack() =>
            new ProvenanceStack(1, Array.Empty<ProvenanceEntry>());

        [Fact]
        public void NormalizeType_Rejects_Backtick_Generic()
        {
            var raw = new RawTypeDto(
                /* Type */ "My.Ns.List`1",
                /* FullName */ null,
                /* Assembly */ null,
                /* TypeKind */ null,
                /* Initializers */ Array.Empty<RawInitializerDto>(),
                /* Attributes */ Array.Empty<string>(),
                /* ImplementedInterfaces */ Array.Empty<string>(),
                /* Assignable */ null,
                /* ProvenanceStack */ MakeProv()
            );

            var errors = new System.Collections.Generic.List<NormalizationError>();
            _ = ConfigurationNormalizer.NormalizeType(raw, EmptyProvenanceStack(), errors);

            Assert.NotEmpty(errors);
            var err = errors.First();
            Assert.Contains("backtick", err.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(err.ProvenanceEntry);
            Assert.Equal("file.yaml", err.ProvenanceEntry!.Origin!.SourcePath);
        }

        [Fact]
        public void NormalizeType_Rejects_AngleBracket_Generic()
        {
            var raw = new RawTypeDto(
                /* Type */ "My.Ns.List<T>",
                /* FullName */ null,
                /* Assembly */ null,
                /* TypeKind */ null,
                /* Initializers */ Array.Empty<RawInitializerDto>(),
                /* Attributes */ Array.Empty<string>(),
                /* ImplementedInterfaces */ Array.Empty<string>(),
                /* Assignable */ null,
                /* ProvenanceStack */ MakeProv(line: 5, logical: "Namespaces[0].Types[1]")
            );

            var errors = new System.Collections.Generic.List<NormalizationError>();
            _ = ConfigurationNormalizer.NormalizeType(raw, EmptyProvenanceStack(), errors);

            Assert.NotEmpty(errors);
            var err = errors.First();
            Assert.Contains("angle-bracket", err.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(err.ProvenanceEntry);
            Assert.Equal(5, err.ProvenanceEntry!.Origin!.LineZeroBased);
        }

        [Fact]
        public void NormalizeType_Rejects_AssemblyTokens_In_Type()
        {
            var raw = new RawTypeDto(
                /* Type */ "My.Ns.Foo, Version=1.0.0.0",
                /* FullName */ null,
                /* Assembly */ null,
                /* TypeKind */ null,
                /* Initializers */ Array.Empty<RawInitializerDto>(),
                /* Attributes */ Array.Empty<string>(),
                /* ImplementedInterfaces */ Array.Empty<string>(),
                /* Assignable */ null,
                /* ProvenanceStack */ MakeProv(line: 10, logical: "Namespaces[0].Types[2]")
            );

            var errors = new System.Collections.Generic.List<NormalizationError>();
            _ = ConfigurationNormalizer.NormalizeType(raw, EmptyProvenanceStack(), errors);

            Assert.NotEmpty(errors);
            var err = errors.First();
            Assert.Contains("assembly qualifiers", err.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(err.ProvenanceEntry);
            Assert.Equal(10, err.ProvenanceEntry!.Origin!.LineZeroBased);
        }

        [Fact]
        public void NormalizeType_Rejects_Missing_Type()
        {
            var raw = new RawTypeDto(
                /* Type */ null,
                /* FullName */ null,
                /* Assembly */ null,
                /* TypeKind */ null,
                /* Initializers */ Array.Empty<RawInitializerDto>(),
                /* Attributes */ Array.Empty<string>(),
                /* ImplementedInterfaces */ Array.Empty<string>(),
                /* Assignable */ null,
                /* ProvenanceStack */ MakeProv(line: 20, logical: "Namespaces[0].Types[3]")
            );

            var errors = new System.Collections.Generic.List<NormalizationError>();
            _ = ConfigurationNormalizer.NormalizeType(raw, EmptyProvenanceStack(), errors);

            Assert.NotEmpty(errors);
            var err = errors.First();
            Assert.Contains("required", err.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(err.ProvenanceEntry);
            Assert.Equal(20, err.ProvenanceEntry!.Origin!.LineZeroBased);
        }

        [Fact]
        public void Normalize_Configuration_Orchestration_PrintsAllErrors_WithProvenance()
        {
            // Build provenance entries for three types
            var provEntry1 = new RawProvenanceEntry(new RawProvenanceOrigin("example.yaml", 3, 2, "Namespaces[0].Types[0]"), "parser", "unit-test", DateTimeOffset.UtcNow);
            var provEntry2 = new RawProvenanceEntry(new RawProvenanceOrigin("example.yaml", 4, 2, "Namespaces[0].Types[1]"), "parser", "unit-test", DateTimeOffset.UtcNow);
            var provEntry3 = new RawProvenanceEntry(new RawProvenanceOrigin("example.yaml", 5, 2, "Namespaces[0].Types[2]"), "parser", "unit-test", DateTimeOffset.UtcNow);

            var namespaceProv = new RawProvenanceStack(Version: 1, Entries: new[] { provEntry1, provEntry2, provEntry3 });

            var rawTypes = new[]
            {
                new RawTypeDto(
                    Type: "MyCompany.Product.List`1",
                    FullName: null,
                    Assembly: null,
                    TypeKind: null,
                    Initializers: Array.Empty<RawInitializerDto>(),
                    Attributes: Array.Empty<string>(),
                    ImplementedInterfaces: Array.Empty<string>(),
                    Assignable: null,
                    ProvenanceStack: new RawProvenanceStack(Version:1, Entries: new[]{ provEntry1 })
                ),
                new RawTypeDto(
                    Type: "MyCompany.Product.Person",
                    FullName: null,
                    Assembly: null,
                    TypeKind: null,
                    Initializers: new[]{ new RawInitializerDto("Init", Eager: false, Order: 0, Parameters: Array.Empty<RawParameterDto>(), ProvenanceStack: new RawProvenanceStack(Version:1, Entries: new[]{ provEntry2 })) },
                    Attributes: Array.Empty<string>(),
                    ImplementedInterfaces: Array.Empty<string>(),
                    Assignable: null,
                    ProvenanceStack: new RawProvenanceStack(Version:1, Entries: new[]{ provEntry2 })
                ),
                new RawTypeDto(
                    Type: "MyCompany.Product.Foo, Version=1.0.0.0",
                    FullName: null,
                    Assembly: null,
                    TypeKind: null,
                    Initializers: Array.Empty<RawInitializerDto>(),
                    Attributes: Array.Empty<string>(),
                    ImplementedInterfaces: Array.Empty<string>(),
                    Assignable: null,
                    ProvenanceStack: new RawProvenanceStack(Version:1, Entries: new[]{ provEntry3 })
                )
            };

            var rawNamespace = new RawNamespaceDto(
                Namespace: "MyCompany.Product",
                Types: rawTypes,
                Interfaces: Array.Empty<RawInterfaceDto>(),
                ProvenanceStack: namespaceProv
            );

            var rawCodeGen = new RawCodeGenDto(
                RegistryClass: "MyCompany.Generated.Registry",
                GeneratedCodePath: "gen",
                ResourceProvider: "rp",
                Namespace: "MyCompany.Generated",
                FailFast: false,
                Enums: Array.Empty<RawCodeGenEnumsDto>(),
                ProvenanceStack: new RawProvenanceStack(Version:1, Entries: new[]{ new RawProvenanceEntry(new RawProvenanceOrigin("example.yaml", 1, 0, "CodeGen"), "parser", "unit-test", DateTimeOffset.UtcNow) })
            );

            var rawConfig = new RawConfigurationDto(
                CodeGen: rawCodeGen,
                Assemblies: Array.Empty<RawAssemblyDto>(),
                NamedInstances: Array.Empty<RawNamedInstanceDto>(),
                Namespaces: new[] { rawNamespace },
                SourcePath: "example.yaml",
                ProvenanceStack: new RawProvenanceStack(Version:1, Entries: new[]{ new RawProvenanceEntry(new RawProvenanceOrigin("example.yaml", 0, 0, "<root>"), "parser", "unit-test", DateTimeOffset.UtcNow) })
            );

            var result = ConfigurationNormalizer.Normalize(rawConfig);

            // Print all errors with provenance for human inspection (xUnit captures console output)
            if (!result.IsSuccess && result.Errors != null)
            {
                foreach (var e in result.Errors)
                {
                    var prov = e.ProvenanceEntry?.Origin;
                    var provText = prov != null ? $"{prov.SourcePath}:{prov.LineZeroBased} ({prov.LogicalPath})" : "<no-provenance>";
                    Debug.WriteLine($"ERROR: {e.Message} -- provenance: {provText}");
                }
            }
// after var result = ConfigurationNormalizer.Normalize(rawConfig);
Debug.WriteLine($"Errors.Count = {result.Errors?.Count ?? 0}");
if (result.Errors != null)
{
    for (int i = 0; i < result.Errors.Count; i++)
    {
        var e = result.Errors[i];
        var prov = e.ProvenanceEntry?.Origin;
        var provText = prov != null ? $"{prov.SourcePath}:{prov.LineZeroBased} ({prov.LogicalPath})" : "<no-provenance>";
        Debug.WriteLine($"[{i}] {e.Message} -- provenance: {provText}");
    }
}

            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Errors);
            Assert.Contains(result.Errors, e => e.Message.Contains("backtick", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Errors, e => e.Message.Contains("assembly qualifiers", StringComparison.OrdinalIgnoreCase));
        }
    }
}

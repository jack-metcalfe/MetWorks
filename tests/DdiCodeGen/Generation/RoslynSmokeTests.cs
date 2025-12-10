using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Tests.Helpers;

namespace DdiCodeGen.Tests
{
    public class RoslynSmokeTests
    {
        [Fact]
        public void RenderedOutput_Compiles_WithDotnetBuild()
        {
            // Arrange: obtain canonical model from your transformer (replace with actual call)
            CanonicalModelDto canonical = GetSampleCanonicalModel();

            // Replace this with the actual generator invocation that produces files.
            // Key: relative path inside generated root; Value: file contents.
            var generatedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Registry.cs"] = "// generated registry code\nnamespace MyNs { public static class Registry { public static void Init() {} } }",
                ["Models/Foo.cs"] = "namespace MyNs { public class Foo { public static string X => \"x\"; } }"
            };

            // Map package references from the canonical model (always non-null in canonical DTO)
            var packageRefs = canonical.CodeGen.PackageReferences
                .Select(p => (Id: p.Id, Version: p.Version ?? string.Empty))
                .ToList();

            // Use the YAML-provided GeneratedCodePath as base; fallback to temp if missing
            var basePath = canonical.CodeGen?.GeneratedCodePath ?? Path.Combine(Path.GetTempPath(), "ddi_codegen");

            // Act & Assert: Create harness, build, and move on success
            string finalFolder;
            try
            {
                // keepWorkingOnFailure = true is useful during development; set to false in CI if you prefer auto-cleanup
                finalFolder = HarnessProjectHelper.CreateAndBuildHarness(basePath, generatedFiles, packageRefs, keepWorkingOnFailure: true);
            }
            catch (InvalidOperationException ex)
            {
                // Fail the test with the build output included
                Assert.Fail($"Compilation failed:\n{ex.Message}");
                throw;
            }

            // Verify expected file exists in final folder
            Assert.True(Directory.Exists(finalFolder), "Final generated folder was not created.");
            Assert.True(File.Exists(Path.Combine(finalFolder, "Registry.cs")), "Expected generated file not found in final folder.");
        }

        private static CanonicalModelDto GetSampleCanonicalModel()
        {
            // Minimal canonical model for the test; replace with real transformer output in integration tests
            var codeGen = new CodeGenDto(
                registryClassName: "Registry",
                generatedCodePath: Path.Combine(Path.GetTempPath(), "ddi_codegen"),
                namespaceName: "MyNs",
                initializerName: "Init",
                packageReferences: Array.Empty<PackageReferenceDto>().ToList().AsReadOnly(),
                provenanceStack: ProvenanceHelper.MakeProvenance("<in-memory>", "codeGen"),
                diagnostics: Array.Empty<DdiCodeGen.Dtos.Canonical.Diagnostic>()
            );

            return new CanonicalModelDto(
                codeGen: codeGen,
                namespaces: Array.Empty<NamespaceDto>(),
                namedInstances: Array.Empty<NamedInstanceDto>(),
                sourcePath: "<in-memory>",
                provenanceStack: ProvenanceHelper.MakeProvenance("<in-memory>", "root"),
                diagnostics: Array.Empty<DdiCodeGen.Dtos.Canonical.Diagnostic>()
            );
        }
    }
}

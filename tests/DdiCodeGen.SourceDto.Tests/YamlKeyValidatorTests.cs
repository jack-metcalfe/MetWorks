using System;
using System.Linq;
using Xunit;
using DdiCodeGen.SourceDto.Internal;
using DdiCodeGen.SourceDto.Raw;

namespace DdiCodeGen.SourceDto.Tests
{
    public class YamlKeyValidatorTests
    {
        private static void RunKeyValidation(string yaml, string sourcePath, out System.Collections.Generic.List<NormalizationError> errors)
        {
            errors = new System.Collections.Generic.List<NormalizationError>();
            YamlKeyValidator.ValidateYamlKeys(yaml, sourcePath, errors);
        }

        [Fact]
        public void UnknownKey_IsReported_WithProvenance()
        {
            var yaml = @"
Namespaces:
  - Namespace: MyCompany.Product
    Types:
      - Type: MyCompany.Product.Person
        GenericArity: 1
";
            RunKeyValidation(yaml, "example.yaml", out var errors);

            Assert.NotEmpty(errors);
            var e = errors.First();
            Assert.Contains("GenericArity", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(e.ProvenanceEntry);
            Assert.Equal("example.yaml", e.ProvenanceEntry!.Origin!.SourcePath);
        }

        [Fact]
        public void MisCasedKey_IsReported()
        {
            var yaml = @"
namespaces:
  - Namespace: MyCompany.Product
    Types:
      - Type: MyCompany.Product.Person
";
            RunKeyValidation(yaml, "example.yaml", out var errors);

            Assert.NotEmpty(errors);
            var e = errors.First();
            Assert.Contains("namespaces", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(e.ProvenanceEntry);
            Assert.Equal("example.yaml", e.ProvenanceEntry!.Origin!.SourcePath);
        }

        [Fact]
        public void ValidKeys_ProduceNoErrors()
        {
            var yaml = @"
Namespaces:
  - Namespace: MyCompany.Product
    Types:
      - Type: MyCompany.Product.Person
";
            RunKeyValidation(yaml, "example.yaml", out var errors);

            Assert.Empty(errors);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DdiCodeGen.Tests
{
    public class CodeGeneratorFixtureTests
    {
        [Fact]
        public void GeneratorProducesFilesFromYamlFixture()
        {
            var store = new InMemoryTemplateStore();
            store.Add("NamedInstanceAccessor.Function.hbs", "// Accessor for {{NamedInstanceName}}");

//            var yaml = File.ReadAllText("fixtures/SimpleModel.yaml");
            var yaml = File.ReadAllText("fixtures/maximal-valid.yaml");
            var model = YamlTestHelper.ParseToCanonical(yaml);

            var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            var gen = new CodeGenerator(store);
            gen.GenerateFiles(model);

            var files = Directory.GetFiles(outDir, "*.g.cs");
            Assert.Contains(files, f => f.Contains("Foo_NamedInstanceAccessor.Function.g.cs"));
        }
    }
}

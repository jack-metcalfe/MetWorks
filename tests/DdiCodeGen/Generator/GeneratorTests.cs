using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using DdiCodeGen.Dtos.Canonical;
using DdiCodeGen.Tests.Helpers;
using Microsoft.VisualBasic;

namespace DdiCodeGen.Tests
{
    public class GeneratorTests
    {
        private readonly Loader loader = new();
        private readonly Transformer transformer = new();
        TemplateStore.TemplateStore templateStore = new();
        [Fact]
        public void Generates_Accessor_For_NamedInstances()
        {
            var yamlText = YamlTestHelper.LoadFixture("maximal-valid.yaml");
            var rawModel = loader.Load(yamlText, "maximal-valid.yaml");
            var canoncialModel = transformer.Transform(rawModel);
            var generator = new CodeGenerator(templateStore);

            generator.GenerateFiles(canoncialModel);

//            Assert.True(result.Files.ContainsKey("TheFileLogger.Accessor.cs"));
        }
    }
}

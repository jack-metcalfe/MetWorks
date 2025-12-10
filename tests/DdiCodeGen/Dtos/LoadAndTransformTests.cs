using System.IO;
using Xunit;
using DdiCodeGen.Dtos.Internal;

public class YamlRawModelLoaderTests
{
    private readonly Loader loader = new();
    private readonly Transformer transformer = new();

    [Fact]
    public void Load_MaximalValidConfig_Works()
    {
        var yaml = YamlTestHelper.LoadFixture("maximal-valid.yaml");
        var rawModel = loader.Load(yaml, "maximal-valid.yaml");
        Assert.Empty(rawModel.Diagnostics);
        Assert.NotNull(rawModel.CodeGen);
        Assert.NotNull(rawModel.CodeGen.NamespaceName);
        Assert.NotNull(rawModel.CodeGen.GeneratedCodePath);
        Assert.NotNull(rawModel.CodeGen.RegistryClassName);
        Assert.NotNull(rawModel.CodeGen.InitializerName);
        Assert.NotNull(rawModel.CodeGen.PackageReferences);
        Assert.NotNull(rawModel.NamedInstances);
        Assert.NotNull(rawModel.Namespaces);

        var canoncialModel = transformer.Transform(rawModel);
    }
}

using YamlDotNet.RepresentationModel;

public static class YamlTestHelper
{
    public static string LoadFixture(string name)
        => File.ReadAllText(Path.Combine("fixtures", name));
    public static CanonicalModelDto ParseToCanonical(string yamlText)
    {
        var loader = new DdiCodeGen.Dtos.Internal.Loader();
        var raw = loader.Load(yamlText, sourcePath: "<test-fixture>");

        var transformer = new DdiCodeGen.Dtos.Transformer();
        return transformer.Transform(raw);
    }
}

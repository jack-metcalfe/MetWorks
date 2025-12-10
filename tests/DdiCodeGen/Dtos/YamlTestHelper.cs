using YamlDotNet.RepresentationModel;

public static class YamlTestHelper
{
    public static string LoadFixture(string name)
        => File.ReadAllText(Path.Combine("fixtures", name));
}

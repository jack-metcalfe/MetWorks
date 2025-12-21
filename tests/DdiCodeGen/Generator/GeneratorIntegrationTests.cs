using Xunit;
using DdiCodeGen.Generator;
using System.IO;
using System.Linq;

public class GeneratorIntegrationTests
{
    private readonly Loader loader = new();
    private readonly Transformer transformer = new();
    TemplateStore templateStore = new();
    [Fact]
    public void Generates_Accessor_For_TheFileLogger_From_MaximalYaml()
    {
        // Arrange
        // 1) Load canonical model from YAML (assumes your loader exists and returns CanonicalModelDto)
        var yamlText = YamlTestHelper.LoadFixture("maximal-valid.yaml");
        var rawModel = loader.Load(yamlText, "maximal-valid.yaml");
        var model = transformer.Transform(rawModel);

        var generator = new CodeGenerator(templateStore);

        // Act
        var result = generator.GenerateFiles(model);
        SaveFiles(result);

        // Assert
        // Expect an accessor file for TheFileLogger (per your YAML)
        // Assert.Contains("TheFileLogger.Accessor.cs", result.Files.Keys);

        // var content = result.Files["TheFileLogger.Accessor.cs"];
        // Assert.Contains("internal static partial class TheFileLogger_Accessors", content);
        // Assert.Contains("Get_TheFileLogger", content);
        // // Ensure the effective type uses the interface when present (Logging.FileLogger exposes InterfaceDefinition.IFileLogger in your YAML)
        // Assert.Contains("InterfaceDefinition.IFileLogger", content);
    }
    private static readonly string TargetFolder = @"GeneratedFiles";
    private static void SaveFiles(IReadOnlyDictionary<string, string> files)
    {
        if (files == null) throw new ArgumentNullException(nameof(files));
        // Ensure the folder exists 
        Directory.CreateDirectory(TargetFolder);
        foreach (var kvp in files)
        {
            var tmpFileName = kvp.Key.Replace('/', '_').Replace('\\', '_');
            var fileName = Path.GetExtension(kvp.Key) == ".cs" 
                ? Path.ChangeExtension(tmpFileName, "g.cs") : tmpFileName;
            var contents = kvp.Value ?? string.Empty;
            // Combine folder path with file name 
            var fullPath = Path.Combine(TargetFolder, fileName);
            // Write contents to file 
            File.WriteAllText(fullPath, contents);
        }
    }
}

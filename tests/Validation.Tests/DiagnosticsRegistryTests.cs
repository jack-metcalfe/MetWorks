using MetWorks.Core.Diagnostics;
using System;
using System.IO;
using Xunit;

namespace MetWorks.Validation.Tests;

public class DiagnosticsRegistryTests
{
    [Fact]
    public void LoadFromFile_WithExplicitPath_LoadsDescriptors()
    {
        // Arrange - write a small diagnostics file to a temp folder and load from it
        var dir = Path.Combine(Path.GetTempPath(), "diag-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "Diagnostics.json");
        File.WriteAllText(path, @"{
  ""SAMPLE_001"": {
    ""Code"": ""SAMPLE_001"",
    ""Severity"": ""Info"",
    ""Message"": ""Sample diagnostic for tests"",
    ""Remediation"": ""No action required""
  }
}");

        // Act
        var registry = DiagnosticsRegistry.LoadFromFile(path);

        // Assert
        Assert.True(registry.TryGet("SAMPLE_001", out var desc));
        Assert.Equal("SAMPLE_001", desc!.Code);
        Assert.Equal("Info", desc.Severity);
        var diag = registry.Create("SAMPLE_001", "test://fixture", new { ElementIndex = 1 });
        Assert.Equal("SAMPLE_001", diag.Code);
        Assert.Equal("Sample diagnostic for tests", diag.Message);
        Assert.Equal("test://fixture", diag.Provenance);
        Assert.NotNull(diag.Context);
        Assert.True(diag.Context!.ContainsKey("ElementIndex"));
    }
}

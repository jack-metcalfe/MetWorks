using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DdiCodeGen.Tests.Helpers
{
    public static class HarnessProjectHelper
    {
        /// <summary>
        /// Writes generated files into a working folder, creates a harness csproj by injecting package references,
        /// runs dotnet build, and on success moves the working folder into finalGeneratedRoot/generated/<guid>.
        /// Returns the final generated folder path on success.
        /// Throws InvalidOperationException or returns false via exceptions/assertions on failure.
        /// </summary>
        public static string CreateAndBuildHarness(
            string baseGeneratedCodePath,
            IReadOnlyDictionary<string, string> generatedFiles,
            IEnumerable<(string Id, string Version)>? packageReferences = null,
            bool keepWorkingOnFailure = false,
            string csprojTemplatePath = "/srv/repos/MetWorks/tests/DdiCodeGen/Generation/fixtures/GeneratedHarness.csproj.tplt")
        {
            if (string.IsNullOrWhiteSpace(baseGeneratedCodePath))
                baseGeneratedCodePath = Path.Combine(Path.GetTempPath(), "ddi_codegen");

            var workRoot = Path.Combine(baseGeneratedCodePath, "work", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workRoot);

            try
            {
                // 1) Write generated files
                foreach (var kv in generatedFiles)
                {
                    var filePath = Path.Combine(workRoot, kv.Key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(filePath, kv.Value ?? string.Empty, Encoding.UTF8);
                }

                // 2) Prepare csproj by reading template and injecting package references
                string csprojText;
                if (!File.Exists(csprojTemplatePath))
                {
                    // Fallback to embedded minimal template if template file not found
                    csprojText = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <!-- PACKAGE_REFERENCES_PLACEHOLDER -->
</Project>";
                }
                else
                {
                    csprojText = File.ReadAllText(csprojTemplatePath);
                }

                var packageBlock = new StringBuilder();
                if (packageReferences != null)
                {
                    var any = false;
                    packageBlock.AppendLine("  <ItemGroup>");
                    foreach (var pr in packageReferences)
                    {
                        if (string.IsNullOrWhiteSpace(pr.Id)) continue;
                        any = true;
                        var versionAttr = string.IsNullOrWhiteSpace(pr.Version) ? string.Empty : $" Version=\"{pr.Version}\"";
                        packageBlock.AppendLine($"    <PackageReference Include=\"{pr.Id}\"{versionAttr} />");
                    }
                    packageBlock.AppendLine("  </ItemGroup>");
                    if (!any) packageBlock.Clear();
                }

                csprojText = csprojText.Replace("<!-- PACKAGE_REFERENCES_PLACEHOLDER -->", packageBlock.ToString());

                var csprojPath = Path.Combine(workRoot, "GeneratedHarness.csproj");
                File.WriteAllText(csprojPath, csprojText, Encoding.UTF8);

                // 3) Run dotnet build
                var buildSucceeded = RunDotnetBuild(workRoot, out var stdout, out var stderr, out var exitCode);

                if (!buildSucceeded)
                {
                    // Surface build logs for debugging
                    var message = new StringBuilder();
                    message.AppendLine($"dotnet build failed with exit code {exitCode}.");
                    message.AppendLine("=== STDOUT ===");
                    message.AppendLine(stdout);
                    message.AppendLine("=== STDERR ===");
                    message.AppendLine(stderr);

                    if (keepWorkingOnFailure)
                    {
                        // Preserve working folder and include its path in the exception message
                        message.AppendLine();
                        message.AppendLine($"Working folder preserved at: {workRoot}");
                        throw new InvalidOperationException(message.ToString());
                    }

                    // Cleanup and throw
                    try { Directory.Delete(workRoot, recursive: true); } catch { /* best-effort */ }
                    throw new InvalidOperationException(message.ToString());
                }

                // 4) Move working folder to final generated folder
                var finalRoot = Path.Combine(baseGeneratedCodePath, "generated");
                Directory.CreateDirectory(finalRoot);
                var dest = Path.Combine(finalRoot, Path.GetFileName(workRoot));
                Directory.Move(workRoot, dest);

                return dest;
            }
            catch
            {
                // On any exception, attempt best-effort cleanup of workRoot if it still exists (unless preserved)
                if (Directory.Exists(workRoot))
                {
                    try { Directory.Delete(workRoot, recursive: true); } catch { /* best-effort */ }
                }
                throw;
            }
        }

        private static bool RunDotnetBuild(string workingDirectory, out string stdout, out string stderr, out int exitCode)
        {
            stdout = string.Empty;
            stderr = string.Empty;
            exitCode = -1;

            var psi = new ProcessStartInfo("dotnet", "build --nologo --configuration Release")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet process");
            stdout = proc.StandardOutput.ReadToEnd();
            stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            exitCode = proc.ExitCode;

            return exitCode == 0;
        }
    }
}

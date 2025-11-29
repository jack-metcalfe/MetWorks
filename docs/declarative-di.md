Declarative DI — Code Generation Reference
Purpose
Document the Declarative DI code‑generation pipeline so contributors can quickly recall how the pieces fit together, where to look, and how to run or debug the generator.

One‑page quick reference
Resource name: ServiceRegistryConfiguration.ServiceRegistry.yaml (embedded resource in Static Data Store assembly)

Static accessor: IStaticDataStoreContract.GetResourceAsStream(path) / GetResourceAsString(path)

Loader: YamlHelpers.ParseStreamToMappingNode(stream) → YamlDtoLoader.LoadConfigurationFromRoot(mappingNode)

Validator: ConfigValidator.ValidateOrThrow(dto) (honors failFast)

Generator: GeneratorOrchestrator.GenerateFromConfig(dto, genPath) → writes .g.cs files

Generated output: Registry.g.cs, NamedInstanceAccessor.g.cs, enum accessors, etc., placed under generatedCodePath (or obj/ for source generator)

Run console generator:

bash
dotnet run --project tools/CodeGenConsole
Exit codes:

0 success

non‑zero for validation/generation failures (respect failFast)

High‑level architecture and flow
Components
Static Data Store — supplies YAML as embedded resource; accessed via IStaticDataStoreContract.

Yaml loader — parses YAML stream into a YamlMappingNode and converts to a strongly typed DTO.

Validator — checks required fields, uniqueness, and domain rules; honors failFast.

Generator orchestrator — consumes DTO and emits generated source files (or returns source strings).

Consumers — either a prebuild console tool (writes files to disk) or a Roslyn source generator (emits compile‑time sources).

Tests — unit tests for loader/validator/generator and an integration build that compiles generated output.

Sequence
Read resource from static store.

Parse YAML to YamlMappingNode.

Convert mapping node to DTO (RootConfig).

Validate DTO; abort if failFast and errors exist.

Resolve generatedCodePath (expand env vars; accept absolute or relative).

Call generator to produce files.

Report generated files and exit.

YAML → DTO mapping
Top level

codeGen → CodeGenSection

registryClass → CodeGenSection.RegistryClass

generatedCodePath → CodeGenSection.GeneratedCodePath

namespace → CodeGenSection.Namespace

failFast → CodeGenSection.FailFast

namedInstanceAccessor.* → accessor class/template settings

assemblies → List<AssemblyEntry> (assembly, fullName, path, primitive)

namespaces → List<NamespaceEntry>

interfaces → InterfaceEntry

types → TypeEntry (with initializers, interfaceKeys, assignable)

namedInstances → List<NamedInstance>

namedInstance / namedInstanceKey → unique key

typeKey → fully qualified type name or primitive indicator

assignmentMode → Initializer | PrimitiveArray | NamedInstanceArray | None

initializerKey → initializer method name

assignments → AssignmentEntry (either value or namedInstanceKey)

exposeAsInterface → interface to register under

eagerLoad → boolean

Key code snippets and helpers
Yaml parsing helper
This helper is the canonical YAML parse routine used by the loader. It throws informative exceptions for empty input, non‑mapping root, or malformed YAML and provides a TryParse variant.

csharp
namespace Utility;
public static class YamlHelpers
{
    /// <summary>
    /// Parse a Stream containing YAML and return the root document as a YamlMappingNode.
    /// Throws an informative exception for empty input, non-mapping root, or malformed YAML.
    /// </summary>
    public static YamlMappingNode ParseStreamToMappingNode(Stream yamlStream, string? sourcePath = null)
    {
        if (yamlStream is null) throw new ArgumentNullException(nameof(yamlStream));
        using var reader = new StreamReader(yamlStream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var yaml = new YamlStream();
        try
        {
            yaml.Load(reader);
        }
        catch (YamlDotNet.Core.YamlException ye)
        {
            var src = sourcePath is null ? "stream" : sourcePath;
            throw new InvalidDataException($"Failed to parse YAML from {src}: {ye.Message}", ye);
        }

        if (yaml.Documents == null || yaml.Documents.Count == 0)
            throw new InvalidDataException("YAML stream contains no documents.");

        var doc = yaml.Documents[0];
        if (doc.RootNode is null)
            throw new InvalidDataException("YAML document has no root node.");

        if (doc.RootNode is YamlMappingNode mapping)
            return mapping;

        var rootType = doc.RootNode.GetType().Name;
        var srcName = sourcePath ?? "stream";
        throw new InvalidDataException($"Expected YAML root to be a mapping node in {srcName} but got {rootType}.");
    }

    /// <summary>
    /// Try-parse variant that returns false on failure and an error message.
    /// The stream position will be advanced as the StreamReader reads; caller may rewind if needed.
    /// </summary>
    public static bool TryParseStreamToMappingNode(Stream yamlStream, out YamlMappingNode? mappingNode, out string? error, string? sourcePath = null)
    {
        mappingNode = null;
        error = null;
        try
        {
            mappingNode = ParseStreamToMappingNode(yamlStream, sourcePath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
Notes

Use sourcePath to include the resource name in error messages.

TryParse is useful for non‑fatal flows where you want to log and continue.

Loader pattern
Production:

csharp
using var yamlStream = IStaticDataStoreContract.GetResourceAsStream(resourcePath)
    ?? throw new FileNotFoundException($"Resource not found: {resourcePath}");

var mapping = YamlHelpers.ParseStreamToMappingNode(yamlStream, resourcePath);
var dto = YamlDtoLoader.LoadConfigurationFromRoot(mapping);
ConfigValidator.ValidateOrThrow(dto);
var genPath = ResolveGeneratedPath(dto.CodeGen?.GeneratedCodePath);
var files = GeneratorOrchestrator.GenerateFromConfig(dto, genPath);
Testable overload (recommended): wrap static access behind IResourceReader and call YamlLoader.LoadFromResource(reader, resourcePath).

Generator contract and generation patterns
Generator input and output
Input: RootConfig DTO and resolved outputPath (string).

Output: List<string> of generated file paths.

Emission patterns
Registry class: AddDeclarativeServices(this IServiceCollection services) that registers types and named instances.

Factory lambdas: for assignmentMode: Initializer generate sp => new Concrete(...) or sp => { /* call initializer */ }.

Expose as interface: services.AddSingleton<IInterface, Concrete>(sp => ...) or services.AddSingleton(typeof(IInterface), sp => ...).

Primitive arrays: services.AddSingleton<string[]>(new[] { "a", "b" });

Named instance arrays: build arrays by resolving named instances or emitting static arrays.

Eager load: emit a small hosted service or startup hook that resolves eager instances at startup.

Diagnostics: if failFast is true, generator should surface missing required fields as errors (console exit or context.ReportDiagnostic for source generator).

Platform considerations for MAUI and multi-target
Emit platform‑agnostic code by default. If platform‑specific wiring is required, guard with #if symbols (for example #if ANDROID or #if WINDOWS).

For MAUI multi‑target project, include YAML as an AdditionalFile and reference generator as an analyzer:

xml
<AdditionalFiles Include="..\di-config\codeGen_.txt" />
<ProjectReference Include="..\DiSourceGenerator\DiSourceGenerator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
Troubleshooting and maintenance checklist
Resource not found

Confirm embedded resource name and assembly.

Test IStaticDataStoreContract.GetResourceAsString(path) in a REPL or small program.

YAML parse errors

Use YamlHelpers.TryParseStreamToMappingNode to capture error message and source path.

Run YAML through a linter or YamlDotNet deserializer to get line/column info.

Missing types at compile

Ensure consumer project references assemblies containing types referenced by typeKey.

If generator emits typeof(Foo), the consumer must compile against Foo's assembly.

Absolute generatedCodePath portability

Prefer relative paths or environment tokens; document that absolute paths are allowed but not recommended.

Eager load not firing

Ensure AddDeclarativeServices() is called early in startup and that generated eager‑load hook is registered (hosted service or startup action).

Schema changes

If YAML schema changes, update DTOs, YamlDtoLoader, ConfigValidator, generator tests, and documentation.

Where to put this doc
Add as docs/declarative-di.md at repo root.

Add a short pointer in README.md linking to docs/declarative-di.md and a one‑line note in CONTRIBUTING.md: “If you change the YAML schema, update DTOs, validator, generator tests, and docs.”

Quick reference (one‑page)
Resource name: ServiceRegistryConfiguration.ServiceRegistry.yaml

Static accessor: IStaticDataStoreContract.GetResourceAsStream(path) / GetResourceAsString(path)

Loader: YamlHelpers.ParseStreamToMappingNode → YamlDtoLoader.LoadConfigurationFromRoot

Validator: ConfigValidator.ValidateOrThrow(dto) (honors failFast)

Generator: GeneratorOrchestrator.GenerateFromConfig(dto, genPath) → writes .g.cs files

Run: dotnet run --project tools/CodeGenConsole

Test: unit tests for loader/validator/generator; integration build for consumer.

--------------------------
Mainline orchestration notes
What this mainline does
Loads the embedded YAML resource ServiceRegistryConfiguration.ServiceRegistry.yaml via IStaticDataStoreContract.

Parses YAML into a YamlMappingNode and converts it to a configuration DTO (configurationDto).

Reads generatedCodePath and namespace from configurationDto.CodeGen.

Creates a TemplateEngine and a diagnostics list, ensures the output directory exists.

Runs a sequence of pipelines that map DTOs → descriptors → token maps → template renders → atomic file writes:

PrimitiveArray pipeline (members → grouped outer files).

NamedInstanceAccessor pipeline (function snippets → accessor class).

Registry pipeline (member fragments → aggregate registry files).

Registration fragments pipeline (fragment files + aggregate registration file).

Initializer pipeline (invokers → aggregate initializer file).

Collects diagnostics throughout and writes generated .g.cs files using FileHelpers.WriteFileAtomically.

Key behaviors and invariants to remember
FailFast is read into globalFailFast and passed into token maps; the orchestration itself uses exceptions and return codes for fatal mapping/rendering failures.

Diagnostics is a single List<string> that accumulates messages across all pipelines and is used for console output and for passing to FileHelpers.WriteFileAtomically.

Identifier uniqueness is validated early via CSharpIdentifierUniquenessValidator.ValidateAll(...) and throws on failure.

Template rendering is defensive: token diagnostics skip rendering; render diagnostics are appended; render failures skip writes.

File naming uses FileSafeNs to sanitize namespace into a file-safe token.

Atomic writes are used for safety when writing generated files.

Potential issues and edge cases to watch
Null assumptions

configurationDto.CodeGen! uses the null-forgiving operator in places; if CodeGen is missing the code will throw earlier, but consider clearer checks and error messages.

Path handling and portability

generatedCodePath is used as-is. If YAML contains absolute Windows paths, CI or other devs may have trouble. Consider expanding environment variables and resolving relative paths to the repo root or working directory.

Large diagnostics list

Diagnostics is unbounded; for very large YAMLs this can grow large. Consider limiting or streaming diagnostics to a log file.

Template errors vs fatal errors

Current flow treats many template/token errors as non-fatal (skip and continue). That’s fine, but make sure CI/consumers can detect when generation was incomplete (exit code, summary).

Concurrency and performance

All pipelines run sequentially. If generation becomes slow, some independent pipelines (e.g., per-group renders) could be parallelized, but be careful with shared diagnostics list (use concurrent collection).

File name sanitization

FileSafeNs replaces . and spaces but other invalid filename characters may remain. Use a stricter sanitizer for file names.

Template injection and token safety

Token values are inserted into templates; ensure templates escape or validate values where needed to avoid invalid C# output.

Error visibility

The code writes diagnostics to diagnostics but I don’t see a final dump to console in this snippet. Ensure the console or CI receives the diagnostics and a non‑zero exit code when appropriate.

Small, practical improvements (copy/paste ready)
1. Resolve generated path robustly
csharp
static string ResolveGeneratedPath(string? configuredPath)
{
    if (string.IsNullOrWhiteSpace(configuredPath))
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Generated"));

    // Expand env vars like %TEMP% or ${REPO_ROOT}
    var expanded = Environment.ExpandEnvironmentVariables(configuredPath);

    // If relative, resolve against current working directory
    if (!Path.IsPathRooted(expanded))
        expanded = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), expanded));

    return expanded;
}
Use this instead of using generatedCodePath directly.

2. Stronger file name sanitizer
csharp
static string FileSafeNs(string ns)
{
    if (string.IsNullOrWhiteSpace(ns)) return "Global";
    var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '.' }).Distinct().ToArray();
    var sb = new StringBuilder(ns);
    foreach (var c in invalid) sb.Replace(c, '_');
    return sb.ToString();
}
3. Centralized diagnostic sink with severity and final reporting
csharp
enum DiagLevel { Info, Warning, Error }

record Diagnostic(DiagLevel Level, string Message);

var diagnostics = new List<Diagnostic>();

void AddDiag(DiagLevel level, string msg) => diagnostics.Add(new Diagnostic(level, msg));

// Example usage:
AddDiag(DiagLevel.Error, $"PrimitiveArray:{d.SafeKey}: render failed; skipping.");
// At end, print summary and set exit code
var errorCount = diagnostics.Count(d => d.Level == DiagLevel.Error);
foreach (var d in diagnostics) Console.WriteLine($"{d.Level}: {d.Message}");
if (errorCount > 0) return 1;
This makes it easier to decide exit codes and to filter messages.

4. Protect shared state if you parallelize
If you parallelize rendering across groups, use thread-safe collections:

csharp
var diagnostics = new ConcurrentBag<Diagnostic>();
var primitiveMemberPairs = new ConcurrentBag<(PrimitiveArrayDescriptor, string)>();
// use Parallel.ForEach for groups and add to concurrent collections
5. Ensure template render failures can be fatal when configured
If FailFast should abort generation on template errors:

csharp
if (configurationDto.CodeGen?.FailFast == true && renderResult.Diagnostics.Any(d => d.IsError))
{
    diagnostics.Add(new Diagnostic(DiagLevel.Error, $"Template {templateName} failed for {d.SafeKey}"));
    // optionally throw or set a fatal flag and break
}
6. Final console output and exit code mapping
At the end of Run:

csharp
// print summary
Console.WriteLine($"Generation completed. Files written to: {generatedCodePath}");
var errors = diagnostics.Count(d => d.Level == DiagLevel.Error);
var warnings = diagnostics.Count(d => d.Level == DiagLevel.Warning);
Console.WriteLine($"Diagnostics: {errors} errors, {warnings} warnings, {diagnostics.Count} total.");
return errors > 0 ? 1 : 0;
Tests and CI checks to add
Unit tests

CSharpIdentifierUniquenessValidator with duplicate and valid keys.

PrimitiveArrayDescriptorBuilder and TokenMapBuilder for expected token maps.

Template rendering stubs: feed a fake ITemplateStore that returns known templates and assert templateEngine.Render results.

FileHelpers.WriteFileAtomically behavior (temp file, rename, permissions).

Integration test

Small sample YAML fixture → run Run(...) in a test harness and assert generated files exist and compile (use dotnet build on a temporary project that includes generated files).

CI

Fail the job if generation produces errors when failFast is true or if diagnostics contain errors above a threshold.

Where to document these details
Add a short section to docs/declarative-di.md:

Runtime behavior: path resolution, diagnostics, exit codes.

How to run locally: dotnet run --project tools/CodeGenConsole and how to inspect diagnostics.

How to add templates: template names used (PrimitiveArray.Member, PrimitiveArray, NamedInstanceAccessor.Function, Registry.Member, Registry, Registration.Fragment, Registration, Initializer.Invoker, Initializer) and required outer tokens.

Quick checklist before committing changes
[ ] Replace direct generatedCodePath usage with ResolveGeneratedPath.

[ ] Sanitize file names with FileSafeNs improvement.

[ ] Convert diagnostics to structured diagnostics with severity.

[ ] Ensure FailFast behavior is honored consistently across mapping/rendering.

[ ] Add final diagnostics summary and meaningful exit code.

[ ] Add unit tests for mappers, builders, and template rendering.

[ ] Add integration test that compiles generated output.
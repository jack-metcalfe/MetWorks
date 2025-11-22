using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MetWorks.Core.Diagnostics;

public sealed record DiagnosticDescriptor
{
    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
    public string? Remediation { get; init; }
}

public sealed class DiagnosticsRegistry
{
    private readonly IReadOnlyDictionary<string, DiagnosticDescriptor> _map;

    private DiagnosticsRegistry(IReadOnlyDictionary<string, DiagnosticDescriptor> map)
    {
        _map = map;
    }

    public static DiagnosticsRegistry LoadFromFile(string? pathHint = null)
    {
        var candidatePaths = new List<string>();

        if (!string.IsNullOrEmpty(pathHint)) candidatePaths.Add(pathHint);

        // conventional locations to look for Diagnostics.json
        candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, "diagnostics", "Diagnostics.json"));
        candidatePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "diagnostics", "Diagnostics.json"));
        candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, "Diagnostics.json"));
        candidatePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "Diagnostics.json"));

        string? found = candidatePaths.FirstOrDefault(File.Exists);
        if (found == null)
        {
            throw new FileNotFoundException("Diagnostics.json not found in known locations.", string.Join(";", candidatePaths));
        }

        using var stream = File.OpenRead(found);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var map = new Dictionary<string, DiagnosticDescriptor>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                try
                {
                    var desc = JsonSerializer.Deserialize<DiagnosticDescriptor>(prop.Value.GetRawText()) ?? new DiagnosticDescriptor();
                    if (string.IsNullOrWhiteSpace(desc.Code)) desc = desc with { Code = prop.Name };
                    map[desc.Code] = desc;
                }
                catch
                {
                    // Skip malformed entries to avoid throwing at load time.
                }
            }
        }

        return new DiagnosticsRegistry(map);
    }

    public bool TryGet(string code, out DiagnosticDescriptor? descriptor)
    {
        if (code == null) { descriptor = null; return false; }
        return _map.TryGetValue(code, out descriptor);
    }

    public Diagnostic Create(string code, string? provenance = null, object? context = null)
    {
        if (!_map.TryGetValue(code, out var desc))
        {
            var msg = $"Unknown diagnostic code {code}";
            return Diagnostic.Make(code, "Error", msg, provenance, context);
        }

        return Diagnostic.Make(desc.Code, desc.Severity, desc.Message, provenance, context);
    }

    public IReadOnlyCollection<DiagnosticDescriptor> AllDescriptors() => _map.Values.ToList();
}

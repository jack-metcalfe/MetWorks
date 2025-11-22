using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MetWorks.Core.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed class Diagnostic
{
    public string Code { get; }
    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public string? Provenance { get; }
    public IReadOnlyDictionary<string, object?>? Context { get; }

    public Diagnostic(string code, DiagnosticSeverity severity, string message, string? provenance = null, IReadOnlyDictionary<string, object?>? context = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Severity = severity;
        Message = message ?? string.Empty;
        Provenance = provenance;
        Context = context;
    }

    // Factory helper to normalize context payloads (accepts JsonElement, IDictionary, or POCO)
    public static Diagnostic Make(string code, string severity, string message, string? provenance = null, object? context = null)
    {
        var sev = severity switch
        {
            "Info" => DiagnosticSeverity.Info,
            "Warning" => DiagnosticSeverity.Warning,
            "Error" => DiagnosticSeverity.Error,
            _ => DiagnosticSeverity.Info
        };

        var normal = NormalizeContext(context);
        return new Diagnostic(code, sev, message, provenance, normal);
    }

    private static IReadOnlyDictionary<string, object?>? NormalizeContext(object? context)
    {
        if (context == null) return null;

        if (context is IReadOnlyDictionary<string, object?> roDict) return roDict;
        if (context is IDictionary<string, object?> dict) return new Dictionary<string, object?>(dict);

        if (context is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in je.EnumerateObject())
            {
                d[prop.Name] = JsonElementToObject(prop.Value);
            }
            return d;
        }

        // Try to read public properties via reflection into a dictionary
        var props = context.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (props.Length == 0)
        {
            return new Dictionary<string, object?> { ["value"] = context };
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in props)
        {
            try
            {
                result[p.Name] = p.GetValue(context);
            }
            catch
            {
                result[p.Name] = null;
            }
        }
        return result;
    }

    private static object? JsonElementToObject(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => e.GetRawText(),
            JsonValueKind.Object => e.GetRawText(),
            _ => e.GetRawText()
        };
    }
}

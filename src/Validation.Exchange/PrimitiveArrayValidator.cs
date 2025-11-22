using MetWorks.Core.Diagnostics;
using MetWorks.Core.Models.Descriptors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MetWorks.Validation.Exchange;

public enum DetectedPrimitiveKind
{
    Unknown,
    Integer,
    Float,
    Boolean,
    String
}

public sealed class PrimitiveArrayValidator
{
    /// <summary>
    /// Validate a sequence of ElementDescriptor intended to represent a primitive array.
    /// If expectedKind is provided, each literal must parse to that primitive kind (or be coercible when allowCoercion is true).
    /// Produces diagnostics using the provided registry. Registry must contain codes:
    /// PA_MIXED_MODE, PA_LITERAL_PARSE_FAILED, PA_EXPECTED_KIND_MISMATCH.
    /// </summary>
    public IReadOnlyList<Diagnostic> Validate(
        IReadOnlyList<ElementDescriptor> elements,
        DiagnosticsRegistry registry,
        string? provenance = null,
        DetectedPrimitiveKind? expectedKind = null,
        bool allowCoercion = false)
    {
        var diags = new List<Diagnostic>();
        if (elements == null || elements.Count == 0) return diags;

        var hasLiteral = elements.Any(e => !string.IsNullOrEmpty(e.Literal));
        var hasTypeKey = elements.Any(e => !string.IsNullOrEmpty(e.TypeKey));

        // Mixed mode check
        if (hasLiteral && hasTypeKey)
        {
            var ctx = new { Count = elements.Count, LiteralCount = elements.Count(e => !string.IsNullOrEmpty(e.Literal)), NamedCount = elements.Count(e => !string.IsNullOrEmpty(e.TypeKey)) };
            diags.Add(registry.Create("PA_MIXED_MODE", provenance, ctx));
            return diags;
        }

        if (hasTypeKey)
        {
            // Nothing else to validate here for primitive arrays of references
            return diags;
        }

        // All-literal mode — detect primitive kind and ensure consistent parsing
        DetectedPrimitiveKind? detectedKind = null;
        for (var i = 0; i < elements.Count; i++)
        {
            var lit = elements[i].Literal ?? string.Empty;

            var kind = DetectLiteralKind(lit, out var normalized);
            if (detectedKind == null || detectedKind == DetectedPrimitiveKind.Unknown)
            {
                detectedKind = kind;
            }
            else if (kind != detectedKind && kind != DetectedPrimitiveKind.Unknown)
            {
                // Disagreement between elements (e.g., int vs float)
                var ctx = new { Index = i, Value = lit, Expected = detectedKind.ToString(), Actual = kind.ToString() };
                diags.Add(registry.Create("PA_LITERAL_PARSE_FAILED", provenance, ctx));
            }

            // Unknown parse for a non-empty literal
            if (kind == DetectedPrimitiveKind.Unknown && !string.IsNullOrEmpty(lit))
            {
                var ctx = new { Index = i, Value = lit, Reason = "Unrecognizable primitive literal" };
                diags.Add(registry.Create("PA_LITERAL_PARSE_FAILED", provenance, ctx));
                continue;
            }

            // If an expectedKind is specified, enforce it (consider allowCoercion)
            if (expectedKind != null && expectedKind != DetectedPrimitiveKind.Unknown)
            {
                if (!IsCompatibleWithExpected(kind, expectedKind.Value, allowCoercion))
                {
                    var ctx = new { Index = i, Value = lit, Expected = expectedKind.ToString(), Actual = kind.ToString() };
                    diags.Add(registry.Create("PA_EXPECTED_KIND_MISMATCH", provenance, ctx));
                }
            }
        }

        return diags;
    }

    private static bool IsCompatibleWithExpected(DetectedPrimitiveKind actual, DetectedPrimitiveKind expected, bool allowCoercion)
    {
        if (actual == expected) return true;

        if (allowCoercion)
        {
            // allow integer -> float coercion when expected is float
            if (actual == DetectedPrimitiveKind.Integer && expected == DetectedPrimitiveKind.Float) return true;
            // allow numeric float -> integer coercion is not allowed without explicit rounding rules
        }

        // allow any kind to be treated as string when expected is string
        if (expected == DetectedPrimitiveKind.String) return true;

        return false;
    }

    /// <summary>
    /// Heuristic detection on a single literal string.
    /// Returns the detected kind and the parsed/normalized object when possible.
    /// </summary>
    private static DetectedPrimitiveKind DetectLiteralKind(string raw, out object? normalized)
    {
        normalized = null;
        if (string.IsNullOrEmpty(raw))
        {
            normalized = string.Empty;
            return DetectedPrimitiveKind.String;
        }

        // Boolean
        if (bool.TryParse(raw, out var b))
        {
            normalized = b;
            return DetectedPrimitiveKind.Boolean;
        }

        // Integer (try Int64)
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            normalized = l;
            return DetectedPrimitiveKind.Integer;
        }

        // Float (double)
        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
        {
            normalized = d;
            return DetectedPrimitiveKind.Float;
        }

        // Fallback to string
        normalized = raw;
        return DetectedPrimitiveKind.String;
    }
}

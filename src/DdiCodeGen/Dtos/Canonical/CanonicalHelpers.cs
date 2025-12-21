namespace DdiCodeGen.Dtos.Canonical;

internal static class CanonicalHelpers
{
    /// <summary>
    /// Generate a deterministic InvokerKey from a qualified class name.
    /// Strategy: replace namespace dots with underscores and join with the short name:
    ///   "My.Namespace.ClassName" -> "My_Namespace_ClassName"
    /// This assumes namespace and class names have already been validated elsewhere.
    /// A tiny safety pass ensures the resulting token is a valid C# identifier.
    /// </summary>
    public static string GenerateInvokerKeyFromQualified(string qualifiedClassName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedClassName))
            return "_MissingInvoker";

        // Use existing string extension helpers if available
        var baseName = qualifiedClassName.ExtractBaseQualifiedName();
        var ns = baseName.SafeExtractNamespace();    // "" when no namespace
        var shortName = baseName.ExtractShortName();

        // Replace dots with underscores for namespace token
        var nsToken = string.IsNullOrWhiteSpace(ns) ? "Global" : ns.Replace('.', '_');

        var candidate = $"{nsToken}_{shortName}";

        // Quick check: if candidate is already a valid identifier, return it
        if (candidate.IsValidIdentifier())
            return candidate;

        // Fallback sanitization: replace invalid chars with '_' and ensure leading char is letter or '_'
        var chars = candidate.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var ch = chars[i];
            if (i == 0)
            {
                if (!(char.IsLetter(ch) || ch == '_')) chars[i] = '_';
            }
            else
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '_')) chars[i] = '_';
            }
        }

        var sanitized = new string(chars);
        if (string.IsNullOrWhiteSpace(sanitized))
            return "_MissingInvoker";

        return sanitized;
    }
    // Helper: safe provenance/location extraction
    public static string SafeLocationFromProvenance(DdiCodeGen.Dtos.Canonical.ProvenanceStack? stack, string fallback)
    {
        try
        {
            if (stack?.Entries != null && stack.Entries.Count > 0)
            {
                var latest = stack.Latest;
                if (latest?.Origin?.LogicalPath is string lp && !string.IsNullOrWhiteSpace(lp))
                    return lp;
            }
        }
        catch { /* swallow and use fallback */ }
        return fallback;
    }

}

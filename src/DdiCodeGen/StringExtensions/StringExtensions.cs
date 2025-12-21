namespace DdiCodeGen.StringExtensions;

/// <summary>
/// Provides string validation extensions for identifiers, qualified names, lifetimes, and provenance.
/// </summary>
public static class StringExtensions
{
    // -----------------------------
    // Identifier Validations
    // -----------------------------

    /// <summary>
    /// Returns true if the string is a valid C# identifier.
    /// </summary>
    public static bool IsValidIdentifier(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Must start with letter or underscore
        if (!(char.IsLetter(value![0]) || value[0] == '_'))
            return false;

        // Remaining chars must be letters, digits, or underscore
        if (value.Skip(1).Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
            return false;

        // Cannot be a C# keyword
        if (!IsKeyword(value))
            return true;
        else
            return false;
    }

    public static bool IsNullToken(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the string follows PascalCase convention.
    /// </summary>
    public static bool IsPascalCase(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return char.IsUpper(value![0]) && !value.Contains(' ');
    }

    /// <summary>
    /// Throws if the string is not a valid identifier.
    /// </summary>
    public static void EnsureValidIdentifier(this string? value, string paramName)
    {
        if (!value.IsValidIdentifier())
            throw new ArgumentException($"'{value ?? "<null>"}' is not a valid C# identifier.", paramName);
    }

    private static bool IsKeyword(string value)
    {
        // Use CodeDom provider to detect keywords; CreateProvider may be expensive so keep short-lived.
        using var provider = CodeDomProvider.CreateProvider("CSharp");
        // provider.IsValidIdentifier returns false for keywords and invalid identifiers.
        // We already validated identifier shape, so a false here indicates a keyword or provider-specific invalidity.
        return !provider.IsValidIdentifier(value);
    }

    // -----------------------------
    // Qualified Name Validations
    // -----------------------------

    /// <summary>
    /// Returns true if the string is a valid qualified name (Namespace.ClassName).
    /// Each segment must be a valid identifier and there must be at least two segments.
    /// </summary>
    public static bool IsQualifiedName(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value!.Split('.');
        if (parts.Length < 2) return false;

        // Each part must be a valid identifier
        return parts.All(p => p.IsValidIdentifier());
    }

    /// <summary>
    /// Ensures the value is a valid qualified name; throws ArgumentException when not.
    /// </summary>
    public static void EnsureQualifiedName(this string? value, string paramName)
    {
        if (!value.IsQualifiedName())
            throw new ArgumentException($"'{value ?? "<null>"}' is not a valid qualified name (Namespace.Type).", paramName);
    }

    /// <summary>
    /// Returns true if the string is a valid interface name (starts with 'I' and followed by uppercase).
    /// </summary>
    public static bool IsInterfaceName(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value!.Length > 1 && value[0] == 'I' && char.IsUpper(value[1]) && value.IsValidIdentifier();
    }

    // -----------------------------
    // Namespace helpers
    // -----------------------------

    /// <summary>
    /// Returns the namespace portion of a qualified name (everything before the last dot).
    /// Returns empty string if no namespace portion exists.
    /// </summary>
    // public static string ExtractNamespace(this string? qualifiedName)
    // {
    //     if (string.IsNullOrWhiteSpace(qualifiedName)) return string.Empty;
    //     var idx = qualifiedName!.LastIndexOf('.');
    //     return idx >= 0 ? qualifiedName.Substring(0, idx) : string.Empty;
    // }
    public static string SafeExtractNamespace(this string? qualified)
    {
        if (string.IsNullOrWhiteSpace(qualified))
            return "<missing>";

        try
        {
            // Normalize to base qualified form first (handles generics/array tokens if your ExtractBaseQualifiedName supports them)
            var baseName = qualified.ExtractBaseQualifiedName()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(baseName))
                return "<missing>";

            var lastDot = baseName.LastIndexOf('.');
            if (lastDot <= 0) // no namespace portion or leading dot
                return "<missing>";

            var ns = baseName.Substring(0, lastDot).Trim();
            return string.IsNullOrWhiteSpace(ns) ? "<missing>" : ns;
        }
        catch
        {
            // Swallow any parsing exceptions and return a stable fallback
            return "<missing>";
        }
    }

    /// <summary>
    /// Returns the short name (last segment) of a qualified name.
    /// If the input has no dot, returns the original string (or empty if null/whitespace).
    /// </summary>
    public static string ExtractShortName(this string? qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName)) return string.Empty;
        var parts = qualifiedName!.Split('.');
        return parts.Length == 0 ? string.Empty : parts[^1];
    }

    /// <summary>
    /// Returns true if every segment of the qualified namespace is a valid identifier.
    /// Does not require there to be more than one segment; use IsQualifiedName when you need at least two segments.
    /// </summary>
    public static bool IsValidNamespace(this string? namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName)) return false;
        var parts = namespaceName!.Split('.');
        if (parts.Length == 0) return false;
        return parts.All(p => p.IsValidIdentifier());
    }

    /// <summary>
    /// Ensures the namespace is valid (each segment is a valid identifier); throws when invalid.
    /// </summary>
    public static void EnsureValidNamespace(this string? namespaceName, string paramName)
    {
        if (!namespaceName.IsValidNamespace())
            throw new ArgumentException($"'{namespaceName ?? "<null>"}' is not a valid namespace (dot-separated identifiers).", paramName);
    }

    // -----------------------------
    // Lifetime Validations
    // -----------------------------

    private static readonly string[] ValidLifetimes = { "Singleton", "Scoped", "Transient" };

    /// <summary>
    /// Returns true if the string is a valid DI lifetime (Singleton, Scoped, Transient).
    /// </summary>
    public static bool IsValidLifetime(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return ValidLifetimes.Contains(value!);
    }

    // -----------------------------
    // Provenance Validations
    // -----------------------------

    /// <summary>
    /// Returns true if provenance metadata is present (non-null, non-empty).
    /// </summary>
    public static bool HasProvenance(this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    // -----------------------------
    // Non-nullability & Uniqueness
    // -----------------------------

    /// <summary>
    /// Returns true if the string is non-null and non-empty.
    /// </summary>
    public static bool IsNonNullable(this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Ensures uniqueness of keys across a collection; throws ArgumentException listing duplicates.
    /// </summary>
    public static void EnsureUniqueKeys(this IEnumerable<string?> keys)
    {
        var duplicates = keys.Where(k => k != null)
                             .GroupBy(k => k!, StringComparer.Ordinal)
                             .Where(g => g.Count() > 1)
                             .Select(g => g.Key)
                             .ToList();

        if (duplicates.Any())
            throw new ArgumentException($"Duplicate keys found: {string.Join(", ", duplicates)}");
    }

    /// <summary>
    /// Backwards-compatible alias for EnsureUniqueKeys.
    /// </summary>
    public static void EnsureUniqueSafeKeys(this IEnumerable<string> safeKeys)
        => EnsureUniqueKeys(safeKeys);

    // -----------------------------
    // Type token helpers (array/nullable modifiers)
    // -----------------------------

    /// <summary>
    /// Parse a type reference token into its base qualified name and modifiers.
    /// Supported forms:
    ///   - Ns.Type        -> (BaseQualifiedName="Ns.Type", IsArray=false, IsContainerNullable=false, IsElementNullable=false)
    ///   - Ns.Type?       -> (BaseQualifiedName="Ns.Type", IsArray=false, IsContainerNullable=true,  IsElementNullable=false)
    ///   - Ns.Type[]      -> (BaseQualifiedName="Ns.Type", IsArray=true,  IsContainerNullable=false, IsElementNullable=false)
    ///   - Ns.Type[]?     -> (BaseQualifiedName="Ns.Type", IsArray=true,  IsContainerNullable=true,  IsElementNullable=false)
    /// The form Ns.Type?[] (nullable element inside array) is intentionally disallowed and treated as invalid
    /// (the method returns an empty BaseQualifiedName to signal parse failure).
    /// </summary>
    public static (string BaseQualifiedName, bool IsArray, bool IsContainerNullable, bool IsElementNullable) ParseTypeRef(this string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (string.Empty, false, false, false);

        var s = token!.Trim();
        bool isArray = false, isContainerNullable = false, isElementNullable = false;

        // 1) Array + container-nullable: Foo[]?
        if (s.EndsWith("[]?", StringComparison.Ordinal))
        {
            isArray = true;
            isContainerNullable = true;
            s = s.Substring(0, s.Length - 3).TrimEnd();
            return (s, isArray, isContainerNullable, isElementNullable);
        }

        // 2) Array: Foo[]
        if (s.EndsWith("[]", StringComparison.Ordinal))
        {
            isArray = true;
            s = s.Substring(0, s.Length - 2).TrimEnd();

            // Disallow Foo?[] (nullable element inside array) — treat as invalid
            if (s.EndsWith("?", StringComparison.Ordinal))
            {
                return (string.Empty, false, false, false);
            }

            return (s, isArray, isContainerNullable, isElementNullable);
        }

        // 3) Non-array container nullable: Foo?
        if (s.EndsWith("?", StringComparison.Ordinal))
        {
            isContainerNullable = true;
            s = s.Substring(0, s.Length - 1).TrimEnd();
            return (s, isArray, isContainerNullable, isElementNullable);
        }

        // 4) Plain type: Foo
        return (s, false, false, false);
    }

    /// <summary>
    /// TryParse variant that returns true when a non-empty base qualified name was produced.
    /// Note: semantic validation (IsQualifiedName) should still be performed by the caller.
    /// </summary>
    public static bool TryParseTypeRef(this string? token,
        out string baseQualifiedName,
        out bool isArray,
        out bool isContainerNullable,
        out bool isElementNullable)
    {
        var result = ParseTypeRef(token);
        baseQualifiedName = result.BaseQualifiedName;
        isArray = result.IsArray;
        isContainerNullable = result.IsContainerNullable;
        isElementNullable = result.IsElementNullable;
        return !string.IsNullOrWhiteSpace(baseQualifiedName);
    }

    /// <summary>
    /// Returns true if the token is a valid qualified type token (base qualified name valid; modifiers allowed).
    /// </summary>
    public static bool IsQualifiedTypeToken(this string? token)
    {
        var (baseName, _, _, _) = ParseTypeRef(token);
        return !string.IsNullOrWhiteSpace(baseName) && baseName.IsQualifiedName();
    }

    /// <summary>
    /// Extract the base qualified name from a token with modifiers (e.g., "Ns.Type[]?" -> "Ns.Type").
    /// Returns empty string when token is null/whitespace.
    /// </summary>
    public static string ExtractBaseQualifiedName(this string? token)
    {
        var (baseName, _, _, _) = ParseTypeRef(token);
        return baseName ?? string.Empty;
    }
    private static readonly HashSet<string> PrimitiveQualifiedTypes = new(StringComparer.Ordinal)
    {
        "System.String",
        "System.Int32",
        "System.Int64",
        "System.Boolean",
        "System.Double",
        "System.Single",
        "System.Decimal",
        "System.Char",
        "System.Byte",
        "System.SByte",
        "System.UInt32",
        "System.UInt64",
        "System.Int16",
        "System.UInt16",
        "System.DateTime",
        "System.CancellationTokenSource",
    };

    public static bool IsPrimitiveQualified(this string? qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName)) return false;
        return PrimitiveQualifiedTypes.Contains(qualifiedName.Trim());
    }
}

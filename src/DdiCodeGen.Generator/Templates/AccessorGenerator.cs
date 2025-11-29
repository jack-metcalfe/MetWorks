namespace DdiCodeGen.Generator.Templates
{
    using System;
    using System.Text.RegularExpressions;
    using DdiCodeGen.Templates.StaticDataStore;

    /// <summary>
    /// Generates accessor source by loading an embedded template from TemplateStore
    /// and performing simple token replacement.
    /// </summary>
    public static class AccessorGenerator
    {
        public static string GenerateAccessor(
            string generatedNamespace,
            string registryClass,
            string namedInstanceKey,
            string typeKey,
            string exposeAsInterface)
        {
            if (string.IsNullOrWhiteSpace(generatedNamespace)) throw new ArgumentException("generatedNamespace is required", nameof(generatedNamespace));
            if (string.IsNullOrWhiteSpace(registryClass)) throw new ArgumentException("registryClass is required", nameof(registryClass));
            if (string.IsNullOrWhiteSpace(namedInstanceKey)) throw new ArgumentException("namedInstanceKey is required", nameof(namedInstanceKey));
            if (string.IsNullOrWhiteSpace(typeKey)) throw new ArgumentException("typeKey is required", nameof(typeKey));
            if (string.IsNullOrWhiteSpace(exposeAsInterface)) throw new ArgumentException("exposeAsInterface is required", nameof(exposeAsInterface));

            var template = TemplateStore.GetTemplate("Accessor");

            var result = ReplaceToken(template, "GeneratedNamespace", generatedNamespace);
            result = ReplaceToken(result, "RegistryClass", registryClass);
            result = ReplaceToken(result, "NamedInstanceKey", SanitizeIdentifier(namedInstanceKey));
            result = ReplaceToken(result, "TypeKey", EscapeStringLiteral(typeKey));
            result = ReplaceToken(result, "ExposeAsInterface", exposeAsInterface);

            return result;
        }

        private static string ReplaceToken(string template, string token, string value)
        {
            // Replace tokens like {{Token}} with the provided value.
            return Regex.Replace(template, $"\\{{\\{{\\s*{Regex.Escape(token)}\\s*\\}}\\}}", value ?? string.Empty);
        }

        private static string SanitizeIdentifier(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Unnamed";
            var sb = new System.Text.StringBuilder();
            var capitalizeNext = true;
            foreach (var ch in input)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = true;
                }
            }
            var result = sb.ToString();
            return string.IsNullOrEmpty(result) ? "Unnamed" : result;
        }

        private static string EscapeStringLiteral(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}

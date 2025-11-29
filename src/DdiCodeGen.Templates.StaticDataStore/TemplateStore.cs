namespace DdiCodeGen.Templates.StaticDataStore
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Simple embedded-template store. Templates are embedded resources under
    /// DdiCodeGen.Templates.StaticDataStore.Templates.{Name}.cs.tplt (or other supported extensions).
    /// </summary>
    public static class TemplateStore
    {
        private static readonly Assembly _assembly = typeof(TemplateStore).Assembly;
        private static readonly ConcurrentDictionary<string, string> _cache = new();

        /// <summary>
        /// Get a template by logical name, e.g. "Accessor".
        /// Throws if the template resource is not found.
        /// </summary>
        public static string GetTemplate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Template name is required", nameof(name));
            return _cache.GetOrAdd(name, LoadTemplate);
        }

        private static string LoadTemplate(string name)
        {
            // Candidate resource name endings we accept (namespace + folder + file)
            var candidates = new[]
            {
                $".Templates.{name}.tplt",
            };

            var resources = _assembly.GetManifestResourceNames();
            foreach (var candidate in candidates)
            {
                var match = Array.Find(resources, r => r.EndsWith(candidate, StringComparison.Ordinal));
                if (match != null)
                {
                    using var stream = _assembly.GetManifestResourceStream(match)
                        ?? throw new InvalidOperationException($"Template resource '{match}' could not be opened.");
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }

            var available = string.Join(", ", resources);
            throw new InvalidOperationException($"Template '{name}' not found. Available resources: {available}");
        }
    }
}

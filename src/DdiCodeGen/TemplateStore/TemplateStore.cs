using System.Reflection;
namespace DdiCodeGen.TemplateStore
{
    using System;
    /// <summary>
    /// Provides access to embedded templates stored in this assembly.
    /// </summary>
    public sealed class TemplateStore : ITemplateStore
    {
        private readonly Assembly _assembly;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateStore"/> class
        /// which provides access to embedded templates in this assembly.
        /// </summary>
        public TemplateStore()
        {
            _assembly = typeof(TemplateStore).Assembly;
        }

        /// <summary>
        /// Gets the content of an embedded template resource by name.
        /// </summary>
        /// <param name="name">The template name without the ".tplt" extension.</param>
        /// <returns>The template content as a string.</returns>
        public string GetTemplate(string name)
        {
            var resourceName = _assembly.GetManifestResourceNames()
                .FirstOrDefault(r => r.Equals($"TemplateStore.Templates.{name}.hbs", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException($"Template '{name}.hbs' not found.");

            using var stream = _assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Lists available embedded template resource names without the ".tplt" extension.
        /// </summary>
        public IEnumerable<string> ListTemplates()
            => _assembly.GetManifestResourceNames()
                .Where(r => r.EndsWith(".hbs", StringComparison.OrdinalIgnoreCase))
                .Select(r => Path.GetFileNameWithoutExtension(r));
    }
}
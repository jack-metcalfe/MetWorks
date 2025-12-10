namespace DdiCodeGen.TemplateStore
{
    using System;
    using System.Collections.Generic;
    /// <summary>
    /// Provides functionality to render templates from an <see cref="ITemplateStore"/> using token replacement.
    /// </summary>
    public sealed class TemplateEngine
    {
        private readonly ITemplateStore _store;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateEngine"/> class with the specified template store.
        /// </summary>
        /// <param name="store">The template store used to retrieve templates.</param>
        public TemplateEngine(ITemplateStore store)
        {
            _store = store;
        }

        /// <summary>
        /// Renders the template with the specified name by replacing tokens with provided values.
        /// </summary>
        /// <param name="templateName">The name of the template to render.</param>
        /// <param name="tokens">A dictionary of token names and their replacement values.</param>
        /// <returns>The rendered template string with tokens replaced.</returns>
        public string Render(string templateName, IDictionary<string, string> tokens)
        {
            var template = _store.GetTemplate(templateName);
            foreach (var kvp in tokens)
            {
                template = template.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            }
            return template;
        }
    }
}
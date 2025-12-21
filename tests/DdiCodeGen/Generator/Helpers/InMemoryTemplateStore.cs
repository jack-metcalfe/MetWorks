internal sealed class InMemoryTemplateStore : ITemplateStore
{
    private readonly Dictionary<string, string> _templates = new();

    public void Add(string name, string content) => _templates[name] = content;

    public string GetTemplate(string name)
    {
        if (_templates.TryGetValue(name, out var content)) return content;
        
        throw new KeyNotFoundException($"Template '{name}' not found in store.");
    }

    public IEnumerable<string> ListTemplates() => _templates.Keys;
}

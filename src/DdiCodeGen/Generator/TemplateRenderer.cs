namespace DdiCodeGen.Generator;

public static class TemplateRenderer
{
    public static string Render(string template, object ctx)
    {
        var compiled  = Handlebars.Compile(template);
        return compiled(ctx);
    }
}
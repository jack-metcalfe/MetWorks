public static class DiagnosticsHelper
{
    public static Diagnostic Create(DiagnosticCode code, string message, string? location = null, DiagnosticSeverity? overrideSeverity = null)
    {
        var severity = overrideSeverity ?? code.GetSeverity();
        return new Diagnostic(code, message, location);
    }

    public static void Add(List<Diagnostic> list, DiagnosticCode code, string message, string? location = null, DiagnosticSeverity? overrideSeverity = null)
    {
        list.Add(Create(code, message, location, overrideSeverity));
    }
}

namespace DdiCodeGen.Dtos;
public enum DiagnosticSeverity { Info, Warning, Error }

public sealed record Diagnostic(
    DiagnosticCode DiagnosticCode,
    string Message,
    string? Location = null
);

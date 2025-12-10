namespace DdiCodeGen.Generation
{
    public sealed record GenerationResult(
        bool Succeeded,
        IReadOnlyList<DdiCodeGen.Dtos.Canonical.Diagnostic> Diagnostics,
        IReadOnlyList<string>? EmittedPaths = null
    );
}

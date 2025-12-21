namespace DdiCodeGen.Dtos;

// Marker interface for canonical DTOs that carry provenance
public interface IHaveProvenance { ProvenanceStack ProvenanceStack { get; } }

public static class DiagnosticsHelper
{
    // Canonical provenance-aware Create (no severity override).
    // Null fallbackLocation means "<unknown>".
    public static Diagnostic Create(DiagnosticCode code, string message, ProvenanceStack? provenance, string? fallbackLocation)
    {
        fallbackLocation ??= "<unknown>";
        var location = CanonicalHelpers.SafeLocationFromProvenance(provenance, fallbackLocation);
        return new Diagnostic(code, message, location);
    }

    // Add to a collection (ICollection<T> is intentionally broad).
    public static void Add(ICollection<Diagnostic> list, DiagnosticCode code, string message, ProvenanceStack? provenance, string? fallbackLocation)
    {
        list.Add(Create(code, message, provenance, fallbackLocation));
    }

    public static void Add(ICollection<Diagnostic> list, DiagnosticCode code, string message, string? fallbackLocation)
    {
        list.Add(Create(code, message, null, fallbackLocation));
    }

    public static void Add(ICollection<Diagnostic> list, DiagnosticCode code, string message, RawProvenanceStack? provenance, string? fallbackLocation)
    {
        list.Add(Create(code, message, ProvenanceHelper.MakeProvenance(provenance), fallbackLocation));
    }

    // Convenience overload when you only have a fallback location (no provenance).
    public static void AddWithoutProvenance(ICollection<Diagnostic> list, DiagnosticCode code, string message, string? fallbackLocation)
        => Add(list, code, message, fallbackLocation: fallbackLocation);

    // -------------------------
    // DTO convenience overloads
    // -------------------------

    // Runtime fallback: accept any object and extract provenance if it implements IHaveProvenance.
    // Parameter order: (code, message, dto, fallbackLocation) — consistent with the generic overload.
    public static Diagnostic CreateFromDto(DiagnosticCode code, string message, object? dto, string? fallbackLocation)
    {
        var prov = dto is IHaveProvenance p ? p.ProvenanceStack : null;
        return Create(code, message, prov, fallbackLocation);
    }

    // Compile-time safe overload: only accepts DTO types that implement IHaveProvenance.
    public static Diagnostic CreateFromDto<TDto>(DiagnosticCode code, string message, TDto? dto, string? fallbackLocation)
        where TDto : class, IHaveProvenance
    {
        var prov = dto?.ProvenanceStack;
        return Create(code, message, prov, fallbackLocation);
    }

    // AddFromDto: generic version that calls the compile-time safe overload when possible.
    // Keep a non-generic overload for callers that only have object.
    public static void AddFromDto<TDto>(ICollection<Diagnostic> list, DiagnosticCode code, string message, TDto? dto, string? fallbackLocation)
        where TDto : class, IHaveProvenance
    {
        list.Add(CreateFromDto(code, message, dto, fallbackLocation));
    }

    public static void AddFromDto(ICollection<Diagnostic> list, DiagnosticCode code, string message, object? dto, string? fallbackLocation)
    {
        list.Add(CreateFromDto(code, message, dto, fallbackLocation));
    }
}
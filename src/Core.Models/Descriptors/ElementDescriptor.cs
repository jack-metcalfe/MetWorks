using System;

namespace MetWorks.Core.Models.Descriptors;

public sealed record ElementDescriptor
{
    // A stable key that identifies a named instance or literal binding in configs
    public string? TypeKey { get; init; }

    // Literal value for primitive elements (string, number, boolean)
    public string? Literal { get; init; }

    // Optional metadata used for provenance, UI hints, or migration
    public string? Description { get; init; }

    // The original source location if available (e.g., file://path#line)
    public string? Provenance { get; init; }
}

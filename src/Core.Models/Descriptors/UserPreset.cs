using System;
using System.Collections.Generic;

namespace MetWorks.Core.Models.Descriptors;

/// <summary>
/// A user-editable preset describing the primary screen layout and widget instances.
/// Designed to be serializable to/from YAML/JSON and used as living documentation.
/// </summary>
public sealed record UserPreset
{
    // A human friendly preset name (e.g., "Living Room Wall")
    public string Name { get; init; } = "Default";

    // Optional identifier (useful for migrations)
    public string? PresetId { get; init; } = Guid.NewGuid().ToString("N");

    // List of widgets placed on the primary screen (order is preserved)
    public IReadOnlyList<WidgetDescriptor>? Widgets { get; init; }

    // Global options, e.g., units, timezone, glance mode
    public IReadOnlyDictionary<string, object?>? Options { get; init; }

    // Version for migration purposes
    public int SchemaVersion { get; init; } = 1;

    // Provenance for where this preset came from (file path, device export, etc.)
    public string? Provenance { get; init; }
}

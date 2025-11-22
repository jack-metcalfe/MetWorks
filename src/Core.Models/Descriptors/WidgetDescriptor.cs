using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MetWorks.Core.Models.Descriptors;

/// <summary>
/// Describes a widget that can be rendered on the primary screen.
/// Keep this shape intentionally small and explicit so YAML/JSON remains readable.
/// </summary>
public sealed record WidgetDescriptor
{
    // Unique id for this widget instance within a preset (not global)
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    // A stable widget type (e.g., TemperatureCard, WindGauge)
    public string WidgetType { get; init; } = string.Empty;

    // Human readable title or label
    public string? Title { get; init; }

    // Data bindings required by the widget, keyed by binding name
    // Values can be ElementDescriptor with TypeKey to reference named providers
    public IReadOnlyDictionary<string, ElementDescriptor>? Bindings { get; init; }

    // Layout hints for UI (grid slot, span, priority)
    public int Column { get; init; } = 0;
    public int Row { get; init; } = 0;
    public int ColumnSpan { get; init; } = 1;
    public int RowSpan { get; init; } = 1;

    // Refresh cadence in seconds; null means default/system cadence
    public int? RefreshSeconds { get; init; }

    // Additional provider-specific options in raw form (keeps descriptors extensible)
    public IReadOnlyDictionary<string, object?>? Options { get; init; }
}

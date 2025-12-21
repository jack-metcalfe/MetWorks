namespace DdiCodeGen.Generator;
public sealed record TemplateSpec(
    string StoreKey,     // key used to fetch from ITemplateStore
    TemplateKind Kind,   // classification
    string OutputBase    // base name for output file(s)
);
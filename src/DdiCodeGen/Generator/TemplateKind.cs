namespace DdiCodeGen.Generator;
public enum TemplateKind
{
    Partial,      // used only as a partial, not rendered directly
    PerInstance,  // rendered once per NamedInstance
    Aggregate     // rendered once for the whole model
}
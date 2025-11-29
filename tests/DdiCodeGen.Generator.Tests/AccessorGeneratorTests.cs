// tests/DdiCodeGen.Generator.Tests/AccessorGeneratorTests.cs
using Xunit;
using DdiCodeGen.Generator.Templates;
using DdiCodeGen.Templates.StaticDataStore;

public class AccessorGeneratorTests
{
    [Fact]
    public void TemplateStore_Returns_Accessor_Template()
    {
        var tpl = TemplateStore.GetTemplate("Accessor");
        Assert.False(string.IsNullOrWhiteSpace(tpl));
        Assert.Contains("Get{{NamedInstanceKey}}", tpl, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratedAccessorContainsInterfaceReturnAndConcreteResolve()
    {
        var ns = "Generated.Namespace";
        var registryClass = "GeneratedRegistry";
        var namedInstanceKey = "MyService";
        var typeKey = "MyNs.MyServiceImpl, MyAssembly";
        var exposeAsInterface = "MyNs.IMyService";

        var generated = AccessorGenerator.GenerateAccessor(ns, registryClass, namedInstanceKey, typeKey, exposeAsInterface);

        Assert.Contains($"public {exposeAsInterface} Get{namedInstanceKey}()", generated);
        Assert.Contains($"ResolveConcrete(\"{typeKey.Replace("\\", "\\\\").Replace("\"", "\\\"")}\")", generated);
    }
}

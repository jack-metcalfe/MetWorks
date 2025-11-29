// tests/DdiCodeGen.SourceDto.Tests/ExposureValidatorTests.cs
namespace DdiCodeGen.SourceDto.Tests
{
    using System;
    using System.Collections.Generic;
    using Xunit;
    using DdiCodeGen.SourceDto.Internal;
    using DdiCodeGen.SourceDto.Canonical;
    using DdiCodeGen.SourceDto.Raw;
    using DdiCodeGen.Generator.Templates;

    public class ExposureValidatorTests
    {
        private static ProvenanceStack CreateSampleProvenance()
        {
            var origin = new ProvenanceOrigin("<in-memory>", 0, null, "root");
            var entry = new ProvenanceEntry(origin, "test", "unit-test", DateTimeOffset.UtcNow);
            return new ProvenanceStack(1, new[] { entry });
        }

        [Fact]
        public void ValidateExposeAs_Fails_When_Interface_Not_Found()
        {
            // Arrange: namespaces contain a concrete type but no interfaces
            var sampleProv = CreateSampleProvenance();

            var types = new List<TypeDto>
            {
                new TypeDto(
                    Key: "ConcreteA",
                    FullName: "MyNs.ConcreteA, MyAssembly",
                    Assembly: "MyAssembly",
                    TypeKind: "Class",
                    GenericArity: 0,
                    GenericParameterNames: Array.Empty<string>(),
                    Initializers: Array.Empty<InitializerDto>(),
                    Attributes: Array.Empty<string>(),
                    ImplementedInterfaces: Array.Empty<string>(),
                    Assignable: false,
                    ProvenanceStack: sampleProv)
            };

            var namespaces = new List<NamespaceDto>
            {
                new NamespaceDto("MyNs", types, Array.Empty<InterfaceDto>(), sampleProv)
            };

            var raw = new RawNamedInstanceDto(
                NamedInstance: "MyInstance",
                TypeKey: "ConcreteA",
                AssignmentMode: "Singleton",
                InitializerKey: null,
                EagerLoad: false,
                ExposeAsInterface: "IMyService",
                FailFast: false,
                Assignments: null,
                Elements: null,
                Provenance: null);

            var canonical = new NamedInstanceDto(
                Key: "MyInstance",
                TypeKey: "ConcreteA",
                AssignmentMode: "Singleton",
                InitializerKey: null,
                EagerLoad: false,
                ExposeAsInterface: "IMyService",
                FailFast: false,
                Assignments: Array.Empty<NamedInstanceAssignmentDto>(),
                Elements: Array.Empty<NamedInstanceElementDto>(),
                ProvenanceStack: sampleProv);

            // Act
            var result = ExposureValidator.ValidateExposeAs(raw, canonical, namespaces, "unit-test");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.NotNull(result.Errors);
            Assert.Contains(result.Errors!, e => e.Message.Contains("does not resolve"));
        }

        [Fact]
        public void ValidateExposeAs_Succeeds_When_Type_Implements_Interface()
        {
            // Arrange
            var sampleProv = CreateSampleProvenance();

            var iface = new InterfaceDto("IMyService", "MyAssembly", sampleProv);

            var types = new List<TypeDto>
            {
                new TypeDto(
                    Key: "ConcreteA",
                    FullName: "MyNs.ConcreteA, MyAssembly",
                    Assembly: "MyAssembly",
                    TypeKind: "Class",
                    GenericArity: 0,
                    GenericParameterNames: Array.Empty<string>(),
                    Initializers: Array.Empty<InitializerDto>(),
                    Attributes: Array.Empty<string>(),
                    ImplementedInterfaces: new[] { "IMyService" },
                    Assignable: false,
                    ProvenanceStack: sampleProv)
            };

            var namespaces = new List<NamespaceDto>
            {
                new NamespaceDto("MyNs", types, new[] { iface }, sampleProv)
            };

            var raw = new RawNamedInstanceDto(
                NamedInstance: "MyInstance",
                TypeKey: "ConcreteA",
                AssignmentMode: "Singleton",
                InitializerKey: null,
                EagerLoad: false,
                ExposeAsInterface: "IMyService",
                FailFast: false,
                Assignments: null,
                Elements: null,
                Provenance: null);

            var canonical = new NamedInstanceDto(
                Key: "MyInstance",
                TypeKey: "ConcreteA",
                AssignmentMode: "Singleton",
                InitializerKey: null,
                EagerLoad: false,
                ExposeAsInterface: "IMyService",
                FailFast: false,
                Assignments: Array.Empty<NamedInstanceAssignmentDto>(),
                Elements: Array.Empty<NamedInstanceElementDto>(),
                ProvenanceStack: sampleProv);

            // Act
            var result = ExposureValidator.ValidateExposeAs(raw, canonical, namespaces, "unit-test");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal("MyInstance", result.Value!.Key);
        }
    }

    public class AccessorGeneratorTests
    {
        [Fact]
        public void GeneratedAccessorContainsInterfaceReturnAndConcreteResolve()
        {
            // Arrange
            var ns = "Generated.Namespace";
            var registryClass = "GeneratedRegistry";
            var namedInstanceKey = "MyService";
            var typeKey = "MyNs.MyServiceImpl, MyAssembly";
            var exposeAsInterface = "MyNs.IMyService";

            // Act
            var generated = AccessorGenerator.GenerateAccessor(ns, registryClass, namedInstanceKey, typeKey, exposeAsInterface);

            // Assert
            Assert.Contains($"public {exposeAsInterface} Get{namedInstanceKey}()", generated);
            Assert.Contains($"ResolveConcrete(\"{typeKey.Replace("\\", "\\\\").Replace("\"", "\\\"")}\")", generated);
        }
    }
}

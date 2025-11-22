using MetWorks.Core.Diagnostics;
using MetWorks.Core.Models.Descriptors;
using MetWorks.Validation.Exchange;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MetWorks.Validation.Tests;

public class PrimitiveArrayValidatorTests
{
    private static string GetDiagnosticsFixturePath()
    {
        // fixtures are copied to test output by the project file,
        // so the file will be in the current directory at runtime.
        var candidate = Path.Combine(Directory.GetCurrentDirectory(), "fixtures", "diagnostics-sample.json");
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException("Expected diagnostics fixture not found in test output.", candidate);
        }
        return candidate;
    }
    [Fact]
    public void AllNumericLiterals_ProducesNoDiagnostics()
    {
        var path = GetDiagnosticsFixturePath();
        var registry = DiagnosticsRegistry.LoadFromFile(path);

        var elems = new List<ElementDescriptor>
        {
            new ElementDescriptor { Literal = "1" },
            new ElementDescriptor { Literal = "42" },
            new ElementDescriptor { Literal = "1000" }
        };

        var v = new PrimitiveArrayValidator();
        var diags = v.Validate(elems, registry, "test://numeric");

        Assert.Empty(diags);
    }

    [Fact]
    public void MixedLiteralAndTypeKey_ProducesMixedModeDiagnostic()
    {
        var path = GetDiagnosticsFixturePath();
        var registry = DiagnosticsRegistry.LoadFromFile(path);

        var elems = new List<ElementDescriptor>
        {
            new ElementDescriptor { Literal = "1" },
            new ElementDescriptor { TypeKey = "SomeProvider" },
            new ElementDescriptor { Literal = "3" }
        };

        var v = new PrimitiveArrayValidator();
        var diags = v.Validate(elems, registry, "test://mixed");

        Assert.Single(diags);
        Assert.Equal("PA_MIXED_MODE", diags[0].Code);
        Assert.NotNull(diags[0].Context);
        Assert.Equal(3, diags[0].Context!["Count"]);
    }

    [Fact]
    public void InconsistentLiteralKinds_ProducesParseFailedDiagnostics()
    {
        var path = GetDiagnosticsFixturePath();
        var registry = DiagnosticsRegistry.LoadFromFile(path);

        var elems = new List<ElementDescriptor>
        {
            new ElementDescriptor { Literal = "1" },
            new ElementDescriptor { Literal = "2.5" }, // float here mixes with integers
            new ElementDescriptor { Literal = "3" }
        };

        var v = new PrimitiveArrayValidator();
        var diags = v.Validate(elems, registry, "test://inconsistent");

        // Expect at least one parse-failed diagnostic (float vs int)
        Assert.Contains(diags, d => d.Code == "PA_LITERAL_PARSE_FAILED");
    }
    [Fact]
    public void ExpectedIntegerKind_AllIntegers_NoDiagnostics()
    {
        var path = GetDiagnosticsFixturePath();
        var registry = DiagnosticsRegistry.LoadFromFile(path);

        var elems = new List<ElementDescriptor>
    {
        new ElementDescriptor { Literal = "10" },
        new ElementDescriptor { Literal = "0" },
        new ElementDescriptor { Literal = "-5" }
    };

        var v = new PrimitiveArrayValidator();
        var diags = v.Validate(elems, registry, "test://expected-int", expectedKind: DetectedPrimitiveKind.Integer);

        Assert.Empty(diags);
    }

    [Fact]
    public void ExpectedIntegerKind_WithFloatLiteral_ProducesExpectedKindMismatch()
    {
        var path = GetDiagnosticsFixturePath();
        var registry = DiagnosticsRegistry.LoadFromFile(path);

        var elems = new List<ElementDescriptor>
    {
        new ElementDescriptor { Literal = "1" },
        new ElementDescriptor { Literal = "2.5" }, // float violates expected integer
        new ElementDescriptor { Literal = "3" }
    };

        var v = new PrimitiveArrayValidator();
        var diags = v.Validate(elems, registry, "test://expected-int-violation", expectedKind: DetectedPrimitiveKind.Integer);

        Assert.Contains(diags, d => d.Code == "PA_EXPECTED_KIND_MISMATCH");
    }

    [Fact]
    public void ExpectedFloatKind_WithIntegerLiteral_AllowCoercion_NoDiagnostics()
    {
        var path = GetDiagnosticsFixturePath();
        var registry = DiagnosticsRegistry.LoadFromFile(path);

        var elems = new List<ElementDescriptor>
    {
        new ElementDescriptor { Literal = "1" },
        new ElementDescriptor { Literal = "2" },
        new ElementDescriptor { Literal = "3" }
    };

        var v = new PrimitiveArrayValidator();
        var diags = v.Validate(elems, registry, "test://expected-float-coerce", expectedKind: DetectedPrimitiveKind.Float, allowCoercion: true);

        Assert.Empty(diags);
    }
}

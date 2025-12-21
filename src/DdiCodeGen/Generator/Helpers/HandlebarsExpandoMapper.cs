using Microsoft.VisualBasic;

namespace DdiCodeGen.Generator.Helpers;

public static class ExpandoPipeline
{
    static CanonicalModelDto? _cachedModel = null;
    static CanonicalModelDto CanonicalModelDto
    {
        get
        {
            if (_cachedModel == null)
                throw new InvalidOperationException("CanonicalModelDto must be set before using ExpandoPipeline.");
            return _cachedModel;
        }
        set
        {
            if (_cachedModel is not null)
                throw new InvalidOperationException("CanonicalModelDto has already been set and cannot be changed.");
            _cachedModel = value;
        }
    }
    // Root entry point
    public static List<ExpandoObject> ToExpandoList(CanonicalModelDto model)
    {
        if (_cachedModel is null) CanonicalModelDto = model;

        return model.NamedInstances
            .Select(src => BuildExpando(src, model))
            .ToList();
    }

    // Compose one ExpandoObject from helpers
    internal static ExpandoObject BuildExpando(NamedInstanceDto src, CanonicalModelDto model)
    {
        var dict = new ExpandoObject() as IDictionary<string, object?>;

        Merge(dict, FlattenRegistryTokens(DeriveRegistryTokens(model)));
        Merge(dict, FlattenIdentityTokens(DeriveIdentityTokens(src)));
        Merge(dict, FlattenInitializationTokens(DeriveInitializationTokens(src)));
        Merge(dict, FlattenInstanceTokens(DeriveInstanceTokens(src)));

        var elementsTokens = DeriveElementsTokens(src);
        Merge(dict, FlattenElementsTokens(elementsTokens));

        var assignmentsTokens = DeriveAssignmentsTokens(src);
        Merge(dict, FlattenAssignmentsTokens(assignmentsTokens));

        dict["HasAssignments"] = src.Assignments?.Count > 0;
        dict["HasElements"] = src.Elements?.Count > 0;

        return (ExpandoObject)dict;
    }


    private static void Merge(IDictionary<string, object?> target, IDictionary<string, object?> source)
    {
        foreach (var kv in source)
            target[kv.Key] = kv.Value;
    }

    // ---------------------------
    // RegistryTokens
    // ---------------------------
    private static IDictionary<string, object?> FlattenRegistryTokens(RegistryTokens t)
    {
        return new Dictionary<string, object?>
        {
            ["RegistryClassName"] = t.RegistryClassName,
            ["GeneratedHeader"] = t.GeneratedHeader,
            ["GeneratedNamespace"] = t.GeneratedNamespace
        };
    }

    // ---------------------------
    // IdentityTokens
    // ---------------------------
    private static IDictionary<string, object?> FlattenIdentityTokens(IdentityTokens t)
    {
        return new Dictionary<string, object?>
        {
            ["NamedInstanceName"] = t.NamedInstanceName,
            ["NamedInstanceQualifiedClassName"] = t.NamedInstanceQualifiedClassName,
            ["InstanceFieldName"] = t.InstanceFieldName
        };
    }

    // ---------------------------
    // InitializationTokens
    // ---------------------------
    private static IDictionary<string, object?> FlattenInitializationTokens(InitializationTokens t)
    {
        return new Dictionary<string, object?>
        {
            ["InstanceInitializationExpression"] = t.InstanceInitializationExpression,
            ["IsArray"] = t.IsArray
        };
    }

    // ---------------------------
    // InstanceTokens
    // ---------------------------
    private static IDictionary<string, object?> FlattenInstanceTokens(InstanceTokens t)
    {
        return new Dictionary<string, object?>
        {
            ["NamedInstanceName"] = t.NamedInstanceName,
            ["NamedInstanceQualifiedClassName"] = t.NamedInstanceQualifiedClassName,
            ["NamedInstanceQualifiedInterfaceName"] = t.NamedInstanceQualifiedInterfaceName,
            ["HasInterface"] = t.HasInterface,
            ["InstanceFieldName"] = t.InstanceFieldName,
            ["ExternalAccessorReturnType"] = t.ExternalAccessorReturnType,
            ["InternalAccessorReturnType"] = t.InternalAccessorReturnType
        };
    }

    // ---------------------------
    // ElementsTokens
    // ---------------------------
    private static IDictionary<string, object?> FlattenElementsTokens(ElementsTokens t)
    {
        return new Dictionary<string, object?>
        {
            ["NamedInstanceQualifiedClassName"] = t.NamedInstanceQualifiedClassName,
            ["IsArray"] = t.IsArray,
            ["ElementType"] = t.ElementType,
            ["Elements"] = t.Elements
                .Select(e => new Dictionary<string, object?>
                {
                    ["AssignmentValue"] = e.AssignmentValue,
                    ["AssignmentNamedInstanceName"] = e.AssignmentNamedInstanceName,
                    ["InitializerArgumentExpression"] = e.InitializerArgumentExpression,
                    ["Provenance"] = e.Provenance,
                    ["Diagnostics"] = e.Diagnostics
                })
                .ToList(),
            ["ElementsConstructionExpression"] = t.ElementsConstructionExpression
        };
    }

    // ---------------------------
    // AssignmentsTokens
    // ---------------------------
    private static IDictionary<string, object?> FlattenAssignmentsTokens(AssignmentsTokens t)
    {
        return new Dictionary<string, object?>
        {
            ["InitializerName"] = t.InitializerName,
            ["Assignments"] = t.Assignments
                .Select(a => new Dictionary<string, object?>
                {
                    ["ParameterName"] = a.ParameterName,
                    ["ParameterTypeQualifiedName"] = a.ParameterTypeQualifiedName,
                    ["InitializerArgumentExpression"] = a.InitializerArgumentExpression,
                    ["AssignmentNamedInstanceName"] = a.AssignmentNamedInstanceName,
                    ["IsReference"] = a.IsReference,
                    ["IsArray"] = a.IsArray,
                    ["IsInterface"] = a.IsInterface,
                    ["Provenance"] = a.Provenance,
                    ["Diagnostics"] = a.Diagnostics
                })
                .ToList()
        };
    }
    // Registry-level tokens
    internal sealed class RegistryTokens
    {
        public required string RegistryClassName { get; init; }
        public required string GeneratedHeader { get; init; }
        public required string GeneratedNamespace { get; init; }
    }
    static Dictionary<string, object?> _registryTokens = new();
    private static RegistryTokens DeriveRegistryTokens(CanonicalModelDto model)
    {
        var codeGen = model.CodeGen
            ?? throw new InvalidOperationException("CodeGen section is missing in the model.");

        return new RegistryTokens
        {
            RegistryClassName = codeGen.RegistryClassName,
            GeneratedHeader = codeGen.GeneratedCodePath,
            GeneratedNamespace = codeGen.NamespaceName
        };
    }

    // Identity tokens
    internal sealed class IdentityTokens
    {
        public required string NamedInstanceName { get; init; }

        // Always the concrete class.
        public required string NamedInstanceQualifiedClassName { get; init; }

        public required string InstanceFieldName { get; init; }
    }
    private static IdentityTokens DeriveIdentityTokens(NamedInstanceDto src)
    {
        return new IdentityTokens
        {
            NamedInstanceName = src.NamedInstanceName,
            NamedInstanceQualifiedClassName = src.QualifiedClassName + (src.IsArray ? "[]" : string.Empty) + @"/* DeriveIdentityTokens */",
            InstanceFieldName = $"_{src.NamedInstanceName}Instance"
        };
    }

    // Initialization tokens
    internal sealed class InitializationTokens
    {
        // Null for element-driven instances; otherwise a default initializer expression.
        public string? InstanceInitializationExpression { get; init; }

        // Whether the instance itself is defined as an array.
        public required bool IsArray { get; init; }
    }
    private static InitializationTokens DeriveInitializationTokens(NamedInstanceDto src)
    {
        string? initExpr = null;

        if (src.Elements is not { Count: > 0 })
        {
            initExpr = AssignmentCoercion.InitForType(src.QualifiedClassName, src.IsArray)
                ?? throw new InvalidOperationException(
                    $"Failed to render default initialization for {src.QualifiedClassName}.");
        }

        return new InitializationTokens
        {
            InstanceInitializationExpression = initExpr,
            IsArray = src.IsArray
        };
    }

    // Elements tokens (new)
    internal sealed class ElementToken
    {
        // Optional literal value (e.g., "psi")
        public string? AssignmentValue { get; init; }

        // Optional reference to another named instance
        public string? AssignmentNamedInstanceName { get; init; }

        // Always required: the fully-formed initializer expression
        public required string InitializerArgumentExpression { get; init; }

        // Always present: provenance string (empty if none)
        public required string Provenance { get; init; }

        // Always present: diagnostics list (empty if none)
        public required IReadOnlyList<string> Diagnostics { get; init; }
    }

    internal sealed class ElementsTokens
    {
        public string NamedInstanceQualifiedClassName { get; init; } = string.Empty;
        public bool IsArray { get; init; }
        public string ElementType { get; init; } = string.Empty;
        public IReadOnlyList<ElementToken> Elements { get; init; } = new List<ElementToken>();
        public string ElementsConstructionExpression { get; init; } = string.Empty;
    }
    private static ElementsTokens DeriveElementsTokens(NamedInstanceDto src)
    {
        if (src.Elements.Count == 0)
        {
            return new ElementsTokens
            {
                NamedInstanceQualifiedClassName = src.QualifiedClassName + (src.IsArray ? "[]" : string.Empty),
                IsArray = src.IsArray,
                ElementType = src.QualifiedClassName,
                Elements = Array.Empty<ElementToken>(),
                ElementsConstructionExpression = src.IsArray
                    ? $"new {src.QualifiedClassName}[] {{ }}"
                    : AssignmentCoercion.InitForType(src.QualifiedClassName, isArray: false)
                        ?? throw new InvalidOperationException(
                            $"Failed to render default initialization for {src.QualifiedClassName}")
            };
        }

        // Always the concrete class of the named instance.
        var concreteType = src.QualifiedClassName;

        // Elements are expressed in the context of the concrete type.
        var elementType = concreteType;

        // If you later add true "collection instances", this flag can come from the DTO.
        var isArray = src.IsArray;

        // Build element tokens
        var elementTokens = src.Elements.Select(e =>
        {
            string? literal = e.AssignmentValue != null
                ? AssignmentCoercion.RenderLiteral(elementType, e.AssignmentValue)
                : null;

            string initializerExpr =
                e.AssignmentNamedInstanceName != null
                    ? $"registry.Get{e.AssignmentNamedInstanceName}_Internal()"
                    : literal ?? throw new InvalidOperationException(
                        $"Element for {src.NamedInstanceName} has neither literal nor named instance.");

            return new ElementToken
            {
                AssignmentValue = literal,
                AssignmentNamedInstanceName = e.AssignmentNamedInstanceName,
                InitializerArgumentExpression = initializerExpr + @"/* DeriveElementsTokens - With Elements */",
                Provenance = e.ProvenanceStack.ToString(),
                Diagnostics = e.Diagnostics.Select(d => d.ToString()).ToList()
            };
        }).ToList();

        // Build the final construction expression
        string constructionExpr;

        if (isArray)
        {
            var joined = string.Join(", ",
                elementTokens.Select(e => e.InitializerArgumentExpression));

            constructionExpr = $"new {elementType}[] {{ {joined} }}";
        }
        else
        {
            if (elementTokens.Count != 1)
                throw new InvalidOperationException(
                    $"Non-array element-driven instance {src.NamedInstanceName} must have exactly one element.");

            constructionExpr = elementTokens[0].InitializerArgumentExpression;
        }

        return new ElementsTokens
        {
            NamedInstanceQualifiedClassName = (concreteType += isArray ? "[]" : string.Empty) + @"/* DeriveElementsTokens - new ElementsTokens*/",
            IsArray = isArray,
            ElementType = elementType,
            Elements = elementTokens,
            ElementsConstructionExpression = constructionExpr
        };
    }

    // Assignments tokens (new)
    static Dictionary<string, Dictionary<string, ParameterDto>> _initializerParameterLookup = new();
    static Dictionary<string, Dictionary<string, ParameterDto>> InitializerParameterLookup
    {
        get
        {
            var namespaceDtos = CanonicalModelDto.Namespaces;

            if (_initializerParameterLookup.Count == 0)
            {
                foreach (var ns in namespaceDtos)
                {
                    foreach (var classDef in ns.Classes)
                    {
                        var qualifiedName = classDef.QualifiedClassName;
                        var paramMap = new Dictionary<string, ParameterDto>();

                        foreach (var param in classDef.InitializerParameters)
                        {
                            paramMap[param.ParameterName] = param;
                        }

                        _initializerParameterLookup[qualifiedName] = paramMap;
                    }
                }
            }
            return _initializerParameterLookup;
        }
    }

    static Dictionary<string, ClassDto> _classLookup = new();
    static Dictionary<string, ClassDto> ClassLookup
    {
        get
        {
            var namespaceDtos = CanonicalModelDto.Namespaces;

            if (_classLookup.Count == 0)
            {
                foreach (var ns in namespaceDtos)
                {
                    foreach (var classDef in ns.Classes)
                    {
                        _classLookup[classDef.QualifiedClassName] = classDef;
                    }
                }
            }
            return _classLookup;
        }
    }
    static Dictionary<string, NamedInstanceDto> _namedInstancesLookup = new();
    static Dictionary<string, NamedInstanceDto> NamedInstanceLookup
    {
        get
        {
            if (_namedInstancesLookup.Count == 0)
            {
                foreach (var ni in CanonicalModelDto.NamedInstances)
                {
                    _namedInstancesLookup[ni.NamedInstanceName] = ni;
                }
            }
            return _namedInstancesLookup;
        }
    }
    internal sealed class AssignmentToken
    {
        // Always present: the parameter name in the initializer.
        public required string ParameterName { get; init; }

        // Always present: fully qualified parameter type (interface if available, else class).
        public required string ParameterTypeQualifiedName { get; init; }

        // Always present: fully-formed C# expression for the initializer argument.
        public required string InitializerArgumentExpression { get; init; }

        // Optional: name of referenced named instance (null for literal assignments).
        public string? AssignmentNamedInstanceName { get; init; }

        // True if this assignment references another named instance.
        public required bool IsReference { get; init; }

        // True if the parameter type is an array (ends with "[]").
        public required bool IsArray { get; init; }

        // True if the parameter type is an interface type.
        public required bool IsInterface { get; init; }

        // Always present: provenance string (empty if none).
        public required string Provenance { get; init; }

        // Always present: diagnostics list (empty if none).
        public required IReadOnlyList<string> Diagnostics { get; init; }
    }
    internal sealed class AssignmentsTokens
    {
        // Always present: the name of the initializer method (e.g., "InitializeAsync").
        public required string InitializerName { get; init; }

        // Always present: list of assignment tokens (empty only if HasAssignments = false).
        public required IReadOnlyList<AssignmentToken> Assignments { get; init; }
    }
    private static AssignmentsTokens DeriveAssignmentsTokens(NamedInstanceDto src)
    {
        var initializerName = CanonicalModelDto.CodeGen.InitializerName
            ?? throw new InvalidOperationException("InitializerName must be set in CodeGenDto.");

        if (src.Assignments.Count == 0)
        {
            return new AssignmentsTokens
            {
                InitializerName = initializerName,
                Assignments = Array.Empty<AssignmentToken>()
            };
        }

        var assignmentTokens = src.Assignments.Select(a =>
        {
            var paramInfo = InitializerParameterLookup[src.QualifiedClassName][a.AssignmentParameterName];
            var paramType = !string.IsNullOrEmpty(paramInfo.QualifiedInterfaceName)
                ? paramInfo.QualifiedInterfaceName
                : paramInfo.QualifiedClassName!;

            // if (paramInfo.IsArray && !paramType.EndsWith("[]", StringComparison.Ordinal))
            //     paramType += "[]";

            var isArray = paramInfo.IsArray;// paramType.EndsWith("[]", StringComparison.Ordinal);
            string expression;

            if (!string.IsNullOrEmpty(a.AssignmentNamedInstanceName))
            {
                var targetInstance = NamedInstanceLookup[a.AssignmentNamedInstanceName];

                if (paramInfo.IsArray)
                {
                    var elementsTokens = DeriveElementsTokens(targetInstance);

                    if (elementsTokens.Elements.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Parameter {a.AssignmentParameterName} is an array, but referenced instance " +
                            $"{a.AssignmentNamedInstanceName} has no elements.");
                    }

                    var elementsJoined = string.Join(", ",
                        elementsTokens.Elements.Select(e => e.InitializerArgumentExpression));

                    var elementType = elementsTokens.ElementType;

                    expression = $"new {elementType}[] {{ {elementsJoined} }}";
                }
                else
                {
                    expression = $"registry.Get{a.AssignmentNamedInstanceName}()";
                }
            }
            else
            {
                expression = AssignmentCoercion.RenderLiteral(paramInfo.QualifiedClassName!, a.AssignmentValue!)
                    ?? throw new InvalidOperationException("Failed to render literal assignment.");

                // expression = AssignmentCoercion.InitForType(paramType, isArray)
                //     ?? throw new InvalidOperationException("Failed to render default initialization.");
            }

            return new AssignmentToken
            {
                ParameterName = a.AssignmentParameterName,
                ParameterTypeQualifiedName = paramType,
                InitializerArgumentExpression = expression + @"/* DeriveAssignmentsTokens */",
                AssignmentNamedInstanceName = a.AssignmentNamedInstanceName,
                IsReference = a.AssignmentNamedInstanceName != null,
                IsArray = isArray,
                IsInterface = !string.IsNullOrEmpty(paramInfo.QualifiedInterfaceName),
                Provenance = a.ProvenanceStack.ToString(),
                Diagnostics = a.Diagnostics.Select(d => d.ToString()).ToList()
            };
        }).ToList();

        return new AssignmentsTokens
        {
            InitializerName = initializerName,
            Assignments = assignmentTokens
        };
    }

    public static IDictionary<string, object?> DeriveAggregateTokens(CanonicalModelDto model)
    {
        var instanceExpandos = model.NamedInstances
            .Select(src => new Dictionary<string, object?>
            {
                ["NamedInstanceName"] = src.NamedInstanceName,
                ["HasAssignments"] = src.Assignments.Count > 0,
                ["HasElements"] = src.Elements.Count > 0
            })
            .ToList();

        return new Dictionary<string, object?>
        {
            ["Instances"] = instanceExpandos,
            ["RegistryClassName"] = model.CodeGen?.RegistryClassName
                ?? throw new InvalidOperationException("RegistryClassName missing in CodeGen."),
            ["GeneratedNamespace"] = model.CodeGen?.NamespaceName
                ?? throw new InvalidOperationException("NamespaceName missing in CodeGen."),
            ["GeneratedHeader"] = model.CodeGen?.GeneratedCodePath
                ?? throw new InvalidOperationException("GeneratedCodePath missing in CodeGen.")
        };
    }
    /// <summary>
    /// Derives accessor tokens for all named instances in the model.
    /// </summary>
    public static IDictionary<string, object?> DeriveAccessorTokens(CanonicalModelDto model)
    {
        var instanceExpandos = model.NamedInstances
            .Select(DeriveInstanceTokens)
            .ToList();

        return new Dictionary<string, object?>
        {
            ["Instances"] = instanceExpandos,
            ["RegistryClassName"] = model.CodeGen.RegistryClassName,
            ["GeneratedNamespace"] = model.CodeGen.NamespaceName,
            ["GeneratedHeader"] = $"// Auto-generated by DdiCodeGen ({DateTime.UtcNow:o})"
        };
    }

    internal sealed class InstanceTokens
    {
        // Always present: the name of the named instance.
        public required string NamedInstanceName { get; init; }

        // Always present: fully-qualified concrete class.
        public required string NamedInstanceQualifiedClassName { get; init; }

        // Optional: fully-qualified interface (null if none).
        public string? NamedInstanceQualifiedInterfaceName { get; init; }

        // True if an interface is defined.
        public required bool HasInterface { get; init; }

        // Always present: backing field name (e.g., _UdpPressureSettingInstance).
        public required string InstanceFieldName { get; init; }

        // External accessor return type (interface if available, else concrete).
        public required string ExternalAccessorReturnType { get; init; }

        // Internal accessor return type (always concrete).
        public required string InternalAccessorReturnType { get; init; }
    }
    /// <summary>
    /// Derives accessor tokens for a single named instance.
    /// </summary>
    private static InstanceTokens DeriveInstanceTokens(NamedInstanceDto src)
    {
        if (string.IsNullOrEmpty(src.QualifiedClassName))
            throw new InvalidOperationException(
                $"Named instance '{src.NamedInstanceName}' must have a QualifiedClassName.");

        var isArray = src.IsArray;
        var arraySuffix = isArray ? "[]" : string.Empty;
        var namedInstanceQualifiedClassName = src.QualifiedClassName + arraySuffix;
        var internalAccessorReturnType = namedInstanceQualifiedClassName;

        var namedInstanceClassDtoReference = ClassLookup[src.QualifiedClassName];
        var hasInterface = namedInstanceClassDtoReference.QualifiedInterfaceName is not null;
        var namedInstanceQualifiedInterfaceName = 
            (
                hasInterface 
                ? namedInstanceClassDtoReference.QualifiedInterfaceName + arraySuffix
                : null
            );

        var externalAccessorReturnType = (hasInterface ? namedInstanceQualifiedInterfaceName : namedInstanceQualifiedClassName)!;

        return new InstanceTokens
        {
            NamedInstanceName = src.NamedInstanceName,
            NamedInstanceQualifiedClassName = namedInstanceQualifiedClassName!,
            NamedInstanceQualifiedInterfaceName = namedInstanceQualifiedInterfaceName,
            HasInterface = hasInterface,
            InstanceFieldName = $"_{src.NamedInstanceName}Instance",
            ExternalAccessorReturnType = externalAccessorReturnType!,
            InternalAccessorReturnType = internalAccessorReturnType
        };
    }
}
using static DdiCodeGen.SyntaxLoader.Models.Schema;
using static DdiCodeGen.Shared.DiagnosticsHelper;

namespace DdiCodeGen.SyntaxLoader;
public sealed partial class Loader
{
    public Model Load(string yamlText)
    {
        // GUARD: Validate input
        ArgumentNullException.ThrowIfNull(yamlText);

        // Logical path has instance names embeded but tokenPath is without instance names
        // Logical path is useful for finding locations in source file and providing context with real data
        // Token path is useful for looking up schema info by context
        // XPath-like need to look up syntax for mimicry of XML for YAML location tracking
        var logicalPath = TokenTypeToName[TokenTypes.root];

        // PARSE: Load YAML, collect parse errors as diagnostics (no throw)
        YamlMappingNode? rootYamlMappingNode = null;
        Location? location = null;
        var diagnostics = new List<Diagnostic>();
        try
        {
            var yamlStream = new YamlStream();
            using var stringReader = new StringReader(yamlText);
            yamlStream.Load(stringReader);
            if (yamlStream.Documents.Count > 0)
            {
                var yamlNode = yamlStream.Documents[0].RootNode;
                location = new Location(
                    yamlNode: yamlNode,
                    logicalPath: logicalPath
                );
                rootYamlMappingNode = yamlNode as YamlMappingNode;
                if (rootYamlMappingNode is null)
                    diagnostics.Add(
                        diagnosticCode: DiagnosticCode.YamlRootNodeNotMapping,
                        location: location
                    );
            }
            else
                diagnostics.Add(
                    diagnosticCode: DiagnosticCode.RootYamlDocumentMissing,
                    location: location
                );
        }

        catch (YamlException yamlException)
        {
            // YAML parse error—convert to diagnostic
            // See https://github.com/aaubry/YamlDotNet/wiki/Overview#yamldotnetrepresentationmodel
            diagnostics.Add(
                diagnosticCode: DiagnosticCode.YamlParseError,
                message: $"YAML parse error: {yamlException.Message}",
                location: new Location(yamlException, logicalPath)
            );
        }

        catch (Exception exception)
        {
            diagnostics.Add(
                diagnosticCode: DiagnosticCode.UnrecognizedToken,
                message: $"Unexpected YAML load error: {exception.Message}",
                location: location
            );
        }

        // VALIDATE: Fail fast on critical errors (no root node)
        if (rootYamlMappingNode is null) return new Model(diagnostics: diagnostics);

        // DELEGATE: Parse the model (all diagnostics accumulate in shared list)
        return ParseModel(
            yamlMappingNode: rootYamlMappingNode,
            incomingDiagnostics: diagnostics
        );
    }
    private Model ParseModel(
        YamlMappingNode yamlMappingNode,
        List<Diagnostic> incomingDiagnostics
    )
    {
        var type = typeof(Model);
        var tokenTypeName = TypeToTokenName[type];
        var logicalPath = $"{tokenTypeName}/";
        var localDiagnostics = new List<Diagnostic>();

        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode,
                dtoType: type,
                logicalPath: logicalPath
            )
        );

        var tokenPath = logicalPath;

        var codeGen = ParseCodeGen(
            yamlMappingNode: yamlMappingNode,
            logicalPath: logicalPath,
            tokenPath: tokenPath,
            incomingDiagnostics: localDiagnostics
        );

        var namespaces = ParseNamespaces(
            yamlMappingNode: yamlMappingNode,
            logicalPath: logicalPath,
            tokenPath: tokenPath,
            incomingDiagnostics: localDiagnostics
        );

        var dictionaries = BuildDictionaries(namespaces, localDiagnostics);
        var instances = new List<Instance>();
        Dictionary<string, Instance> instanceDictionary = new();
        if (localDiagnostics.Count == 0)
        {
            instances = ParseInstances(
                yamlMappingNode: yamlMappingNode,
                logicalPath: logicalPath,
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                parameterDictionary: dictionaries.ParameterDictionary,
                classDictionary: dictionaries.ClassDictionary,
                instanceDictionary: instanceDictionary
            );            
        }

        incomingDiagnostics.AddRange(localDiagnostics);

        return new Model(
            codeGen: codeGen,
            namespaces: namespaces,
            instances: instances,
            namespaceDictionary: dictionaries.NamespaceDictionary,
            interfaceDictionary: dictionaries.InterfaceDictionary,
            classDictionary: dictionaries.ClassDictionary,
            parameterDictionary: dictionaries.ParameterDictionary,
            instanceDictionary: instanceDictionary,
            location: new Location(
                yamlNode: yamlMappingNode,
                logicalPath: logicalPath
            ),
            diagnostics: localDiagnostics
        );
    }
    private CodeGen? ParseCodeGen(
        YamlMappingNode yamlMappingNode,
        string logicalPath, 
        string tokenPath,
        List<Diagnostic> incomingDiagnostics
    )
    {
        var type = typeof(CodeGen);
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var codeGenMappingNode = GetChildMapping(
            yamlMappingNode: yamlMappingNode,
            key: tokenTypeName
        );
        if (codeGenMappingNode is null)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.CodeGenMissing,
                location: location
            );

            return null;
        }

        yamlMappingNode = codeGenMappingNode;
       
        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode,
                dtoType: type,
                logicalPath: logicalPath
            )
        );

        tokenTypeName = TokenTypeToName[TokenTypes.codeGenRegistryClass];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var registryClass = GetScalar(
            yamlMappingNode: yamlMappingNode,
            key: tokenTypeName
        );
        if (string.IsNullOrWhiteSpace(registryClass))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.CodeGenMissingRegistryClass,
                location: location
            );
        }
        else if (!registryClass.IsValidIdentifier())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"{tokenTypeName} '{registryClass}' is not a valid identifier.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{registryClass}']" }
            );
        }
        else if (!registryClass.IsPascalCase())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.IdentifierNotPascalCase,
                message: $"{tokenTypeName} '{registryClass}' is not in PascalCase.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{registryClass}']" }
            );
        }

        tokenTypeName = TokenTypeToName[TokenTypes.codeGenCodePath];
        var codePath = GetScalar(
            yamlMappingNode: yamlMappingNode,
            key: tokenTypeName
        );
        if (string.IsNullOrWhiteSpace(codePath))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.CodeGenMissingGeneratedPath,
                message: null,
                location: location
            );
        }

        tokenTypeName = TokenTypeToName[TokenTypes.codeGenNamespace];
        var codeGenNamespace = GetScalar(
            yamlMappingNode: yamlMappingNode,
            key: tokenTypeName
        );
        if (string.IsNullOrWhiteSpace(codeGenNamespace))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.CodeGenMissingNamespace,
                message: null,
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='']" }
            );
        }
        else if (!codeGenNamespace.IsValidNamespace())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.NamespaceInvalidSegment,
                message: $"{tokenTypeName} '{codeGenNamespace}' is not a valid namespace.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{codeGenNamespace}']" }
            );
        }

        tokenTypeName = TokenTypeToName[TokenTypes.codeGenInitializer];
        var initializer = GetScalar(
            yamlMappingNode: yamlMappingNode,
            key: tokenTypeName
        );
        if (string.IsNullOrWhiteSpace(initializer))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.CodeGenMissingInitializer,
                location: location
            );
        }
        else if (!initializer.IsValidIdentifier())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"{tokenTypeName} '{initializer}' is not a valid identifier.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{initializer}']" }
            );
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return new CodeGen(
            registryClass: registryClass,
            codePath: codePath,
            @namespace: codeGenNamespace,
            initializer: initializer,
            location: location,
            diagnostics: localDiagnostics
        );
    }

    private List<Namespace> ParseNamespaces(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics
    )
    {
        var tokenTypeName = TokenTypeToName[TokenTypes.namespaces];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var list = new List<Namespace>();
        var yamlSequenceNode = GetChildSequence(yamlMappingNode, tokenTypeName);
        if (yamlSequenceNode is null)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.NamespacesMissing,
                location: location
            );

            return list;
        }

        for (int i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var childLogicalPath = $"{logicalPath}.{tokenTypeName}[{i}]";
            var childNode = yamlSequenceNode.Children[i];
            var childYamlMappingNode = childNode as YamlMappingNode;
            if (childYamlMappingNode is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.NamespaceInvalidNode,
                    message: $"Namespace at {childLogicalPath}[{i}] must be a mapping node.",
                    location: new Location(childNode, $"{logicalPath}[{i}]")
                );
                continue;
            }

            var dto = ParseNamespace(
                childYamlMappingNode, 
                childLogicalPath, 
                tokenPath,
                localDiagnostics
            );

            if (dto is not null) list.Add(dto);
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return list;
    }
    private Namespace? ParseNamespace(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics
    )
    {
        var type = typeof(Namespace);
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();
        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode,
                dtoType: type,
                logicalPath: logicalPath
            )
        );

        tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceName];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var name = GetScalar(yamlMappingNode, tokenTypeName);
        location = new Location(yamlMappingNode, logicalPath);
        if (string.IsNullOrWhiteSpace(name))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.NamespaceMissingName,
                message: $"Missing '{tokenTypeName}' in {logicalPath}.",
                location: location
            );
        }
        else if (!name.IsValidNamespace())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"Namespace name '{name}' is not a valid identifier.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{name}']" }
            );
        }

        IReadOnlyList<Interface> interfaces = Array.Empty<Interface>();
        IReadOnlyList<Class> classes = Array.Empty<Class>();
        if (name is not null)
        {
            interfaces = ParseInterfaces(
                yamlMappingNode: yamlMappingNode, 
                logicalPath: logicalPath, 
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                namespaceName: name
            );

            classes = ParseClasses(
                yamlMappingNode: yamlMappingNode, 
                logicalPath: logicalPath, 
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                namespaceName: name
            );
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return new Namespace(
            namespaceName: name!,
            interfaces: interfaces,
            classes: classes,
            location: location,
            diagnostics: localDiagnostics
        );
    }
    List<Interface> ParseInterfaces(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        string namespaceName
    )
    {
        var tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceInterfaces];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var list = new List<Interface>();
        var yamlSequenceNode = GetChildSequence(yamlMappingNode, tokenTypeName);
        if (yamlSequenceNode is null)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InterfacesMissing,
                location: location
            );

            return list;
        }

        for (int i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var childLogicalPath = $"{logicalPath}.{tokenTypeName}[{i}]";
            var childNode = yamlSequenceNode.Children[i];
            var childYamlMappingNode = childNode as YamlMappingNode;
            if (childYamlMappingNode is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.InterfaceInvalidNode,
                    message: $"Interface at {childLogicalPath} must be a mapping node.",
                    location: new Location(childNode, childLogicalPath)
                );
                continue;
            }

            var dto = ParseInterface(
                yamlMappingNode: childYamlMappingNode, 
                logicalPath: logicalPath, 
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                namespaceName: namespaceName
            );

            if (dto is not null) list.Add(dto);
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return list;
    }
    private Interface? ParseInterface(
        YamlMappingNode yamlMappingNode, 
        string logicalPath, 
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        string namespaceName
    )
    {
        var type = typeof(Interface);
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();
        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode, 
                dtoType: type, 
                logicalPath: logicalPath
            )
        );

        tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceInterfaceName];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var name = GetScalar(yamlMappingNode, tokenTypeName);
        if (string.IsNullOrWhiteSpace(name))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InterfaceMissingName,
                message: $"Interface name token at {logicalPath} must be a scalar or mapping node.",
                location: location
            );
        }
        else if (!name.IsValidIdentifier())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"InterfaceName '{name}' must be a simple identifier.",
                location: location
            );
        }
        else if (!name.IsInterfaceName())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InterfaceNameInvalidFormat,
                message: $"InterfaceName '{name}' is not in the correct format. Expected format is 'I' followed by a valid identifier.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{name}']" }
            );
        }
        else
            location = location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{name}']" };

        incomingDiagnostics.AddRange(localDiagnostics);
        return new Interface(
            namespaceName: namespaceName,
            interfaceName: name,
            location: location,
            diagnostics: localDiagnostics
        );
    }
    IReadOnlyList<Class> ParseClasses(
        YamlMappingNode yamlMappingNode, 
        string logicalPath, 
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        string namespaceName
    )
    {
        var type = typeof(Class);
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var list = new List<Class>();
        var yamlSequenceNode = GetChildSequence(yamlMappingNode, tokenTypeName);
        if (yamlSequenceNode is null)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.ClassesMissing,
                location: location
            );

            return list;
        }

        for (int i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var childLogicalPath = $"{logicalPath}.{tokenTypeName}[{i}]";
            var childNode = yamlSequenceNode.Children[i];
            var childYamlMappingNode = childNode as YamlMappingNode;
            if (childYamlMappingNode is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.ClassInvalidNode,
                    message: $"Class at {childLogicalPath} must be a mapping node.",
                    location: new Location(childNode, childLogicalPath)
                );
                continue;
            }

            var dto = ParseClass(
                yamlMappingNode: childYamlMappingNode, 
                logicalPath: childLogicalPath, 
                tokenPath: tokenPath, 
                incomingDiagnostics: localDiagnostics,
                namespaceName: namespaceName
            );

            if (dto is not null) list.Add(dto);
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return list;
    }
    private Class? ParseClass(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        string namespaceName
    )
    {
        var type = typeof(Class);        
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();
        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode, 
                dtoType: type, 
                logicalPath: logicalPath
            )
        );

        tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceClassName];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var className = GetScalar(yamlMappingNode, tokenTypeName);
        if (string.IsNullOrWhiteSpace(className))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.ClassMissingName,
                message: $"Missing '{tokenTypeName}' in {logicalPath}.",
                location: location
            );
        }
        else if (!className.IsValidIdentifier())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"ClassName '{className}' is not a valid identifier.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{className}']" }
            );
        }
        else if (!className.IsPascalCase())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.IdentifierNotPascalCase,
                message: $"ClassName '{className}' is not in PascalCase.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{className}']" }
            );
        }
        else if (className.IsQualifiedName())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.ClassNameMustBeSimple,
                message: $"ClassName '{className}' is not in the correct format. Expected format is a valid identifier ending with 'Class'.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{className}']" }
            );
        }
        else
            location = location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{className}']" };

        tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceClassInterface];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var interfaceName = GetScalar(yamlMappingNode, tokenTypeName);
        if (!interfaceName.IsWhiteSpace())
        {
            if (!interfaceName.TryParseTypeRef(
                out var baseName,
                out var isArray,
                out var isContainerNullable,
                out var isElementNullable))
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.TypeRefInvalid,
                    message: $"Invalid type reference '{interfaceName}' at {logicalPath}.",
                    location: location
                );
            }
        }

        var parameters = ParseParameters(
            yamlMappingNode: yamlMappingNode, 
            logicalPath: logicalPath, 
            tokenPath: tokenPath,
            incomingDiagnostics: localDiagnostics,
            namespaceName: namespaceName,
            className: className!
        );

        incomingDiagnostics.AddRange(localDiagnostics);

        return new Class(
            namespaceName: namespaceName,
            className: className,
            interfaceQualified: interfaceName,
            parameters: parameters,
            location: new Location(yamlMappingNode, logicalPath),
            diagnostics: localDiagnostics
        );
    }
    Dictionary<string, Parameter> ParseParameters(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        string namespaceName,
        string className
    )
    {
        var tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceClassParameters];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var dictionary = new Dictionary<string, Parameter>();
        var yamlSequenceNode = GetChildSequence(yamlMappingNode, tokenTypeName);
        if (yamlSequenceNode is null)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.ParametersMissing,
                location: location
            );

            return dictionary;
        }

        for (int i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var childLogicalPath = $"{logicalPath}.{tokenTypeName}[{i}]";
            var childNode = yamlSequenceNode.Children[i];
            var childYamlMappingNode = childNode as YamlMappingNode;
            if (childYamlMappingNode is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.ParameterInvalidNode,
                    message: $"Initializer parameter at {childLogicalPath} must be a mapping node.",
                    location: new Location(childNode, childLogicalPath)
                );
                continue;
            }

            var dto = ParseParameter(
                yamlMappingNode: childYamlMappingNode, 
                logicalPath: childLogicalPath, 
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                namespaceName: namespaceName,
                className: className
            );

            if (dto is not null && !dto.ParameterName.IsWhiteSpace()) 
                dictionary.Add(dto.ParameterName!, dto);
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        
        return dictionary;
    }
    private Parameter? ParseParameter(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        string namespaceName,
        string className
    )
    {
        var type = typeof(Parameter);
        var tokenTypeName = TypeToTokenName[type];
        var incomingLogicalPath = logicalPath;
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();
        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode, 
                dtoType: type, 
                logicalPath: logicalPath
            )
        );  

        tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceClassParameterName];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);        
        var name = GetScalar(yamlMappingNode, tokenTypeName);
        if (string.IsNullOrWhiteSpace(name))
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.ParameterMissingName,
                message: $"Missing 'parameterName' in {logicalPath}.",
                location: location
            );
        }
        else if (!name.IsValidIdentifier())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"ParameterName '{name}' is not a valid identifier.",
                location: location
            );
        }

        tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceClassParameterClass];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);

        var hasClass = false;
        string? classQualified = null;
        bool classIsArray = false;
        bool classIsContainerNullable = false;
        bool classIsElementNullable = false;

        var classToken = GetScalar(yamlMappingNode, tokenTypeName);
        if (!string.IsNullOrWhiteSpace(classToken))
        {
            // ToDo: Add test for generics "<T>" and add diagnostic for them when found
            if (
                classToken.TryParseTypeRef(
                    out classQualified,
                    out classIsArray,
                    out classIsContainerNullable,
                    out classIsElementNullable
                )
            )
                hasClass = true;
            else
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.TypeRefInvalid,
                    message: $"Invalid type reference '{classToken}' at {logicalPath}.",
                    location: location
                );
            }
        }

        tokenTypeName = TokenTypeToName[TokenTypes.namespacesNamespaceClassParameterInterface];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);

        var hasInterface = false;
        string? interfaceQualified = null;
        bool interfaceIsArray = false;
        bool interfaceIsContainerNullable = false;
        bool interfaceIsElementNullable = false;

        var interfaceToken = GetScalar(yamlMappingNode, tokenTypeName);
        if (!string.IsNullOrWhiteSpace(interfaceToken))
        {
            if (
                interfaceToken.TryParseTypeRef(
                    out interfaceQualified,
                    out interfaceIsArray,
                    out interfaceIsContainerNullable,
                    out interfaceIsElementNullable
                )
            )
                hasInterface = true;
            else
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.TypeRefInvalid,
                    message: $"Invalid type reference '{interfaceToken}' at {logicalPath}.",
                    location: location
                );
            }
        }

        if (hasClass && hasInterface)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.ParameterBothClassAndInterface,
                message: $"Parameter at {logicalPath} specifies both qualifiedClassName and qualifiedInterfaceName. Exactly one must be non-null.",
                location: location
            );
        }
        else if (!hasClass && !hasInterface)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.ParameterMissingClassOrInterface,
                message: $"Parameter at {logicalPath} must specify either qualifiedClassName or qualifiedInterfaceName.",
                location: location
            );
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        // ToDo: Add isValid to each dto can keep data found but indicate it's invalid
        // ToDo: Consider adding an List of Diagnostic to each dto to capture dto-specific issues copy those being added to diagnostics or better add to a new diagnostics for new dto then use AddRange to copy to before returning
        // ToDo: Consider combining isArray, isNullable, isElementNullable into a single struct to reduce parameter count
        // ToDo: Consider making isArray, isNullable, isElementNullable non-nullable with default false to reduce null checks
        return new Parameter(
            namespaceName: namespaceName,
            className: className,
            parameterName: name,
            classToken: classToken,
            @class: classQualified.ExtractShortName(),
            classQualified: classQualified,
            interfaceToken: interfaceToken,
            @interface: interfaceQualified.ExtractShortName(),
            interfaceQualified: interfaceQualified,
            isArray: hasInterface ? interfaceIsArray : classIsArray,
            isNullable: hasInterface ? interfaceIsContainerNullable : classIsContainerNullable,
            isElementNullable: hasInterface ? interfaceIsElementNullable : classIsElementNullable,
            location: new Location(yamlMappingNode, logicalPath),
            diagnostics: localDiagnostics
        );
    }
    List<Instance>? ParseInstances(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        Dictionary<string, Parameter> parameterDictionary,
        Dictionary<string, Class> classDictionary,
        Dictionary<string, Instance> instanceDictionary
    )
    {
        var type = typeof(Instance);
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var list = new List<Instance>();
        var yamlSequenceNode = GetChildSequence(yamlMappingNode, tokenTypeName);
        if (yamlSequenceNode is null)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InstanceMissing,
                location: new Location(yamlMappingNode, tokenTypeName)
            );

            return list;
        }

        for (int i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var childLogicalPath = $"{logicalPath}.{tokenTypeName}[{i}]";
            var childNode = yamlSequenceNode.Children[i];
            var childYamlMappingNode = childNode as YamlMappingNode;
            if (childYamlMappingNode is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.InstanceInvalidNode,
                    message: $"NamedInstance at {childLogicalPath} must be a mapping node.",
                    location: new Location(childNode, childLogicalPath)
                );
                continue;
            }

            var dto = ParseInstance(
                yamlMappingNode: childYamlMappingNode,
                logicalPath: childLogicalPath,
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                parameterDictionary: parameterDictionary,
                classDictionary: classDictionary,
                instanceDictionary: instanceDictionary
            );

            if (dto is not null) list.Add(dto);
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return list;
    }
    Instance ParseInstance(
        YamlMappingNode yamlMappingNode, 
        string logicalPath, 
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        Dictionary<string, Parameter> parameterDictionary,
        Dictionary<string, Class> classDictionary,
        Dictionary<string, Instance> instanceDictionary
    )
    {
        var type = typeof(Instance);
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();
        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode, 
                type, 
                logicalPath
            )
        );

        tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceName];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var instanceName = GetScalar(yamlMappingNode, tokenTypeName);
        if (instanceName.IsWhiteSpace())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InstanceMissingName,
                message: $"Missing 'namedInstanceName' in {logicalPath}.",
                location: location
            );
        }
        else if (!instanceName.IsValidIdentifier())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"NamedInstanceName '{instanceName}' is not a valid identifier.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{instanceName}']" }
            );
        }
        else if (!instanceName.IsPascalCase())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.IdentifierNotPascalCase,
                message: $"NamedInstanceName '{instanceName}' is not in PascalCase.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{instanceName}']" }
            );
        }

        tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceClass];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);

        var classToken = GetScalar(yamlMappingNode, tokenTypeName);
        string? classQualified = null;
        bool instanceIsArray = false;
        bool classIsContainerNullable = false;
        bool classIsElementNullable = false;
        Class? instanceClass = null;
        if (classToken.IsWhiteSpace())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InstanceMissingQualifiedClass,
                message: $"Missing 'qualifiedClassName' in {logicalPath}.",
                location: location
            );
        }
        else if (
            !classToken.TryParseTypeRef(
                out classQualified,
                out instanceIsArray,
                out classIsContainerNullable,
                out classIsElementNullable
            )
        )
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.TypeRefInvalid,
                message: $"Invalid type reference '{classToken}' in " +
                $"{logicalPath}. Supported forms: 'Ns.Type', 'Ns.Type?', " +
                "'Ns.Type[]', 'Ns.Type[]?'. Nullable element types inside " +
                "arrays (e.g., 'Ns.Type?[]') are not supported.",
                location: location
            );
        }
        else
        {
            instanceClass = classDictionary.GetValueOrDefault(classQualified!);
            if (instanceClass is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.InstanceClassNotFound,
                    message: $"Instance class '{classQualified}' not found for " +
                             $"named instance '{instanceName}' in {logicalPath}.",
                    location: location
                );
            }
        }

        if (instanceClass is null)
        {
            throw new InvalidOperationException(
                $"Cannot continue parsing instance '{instanceName}' because " +
                "its class could not be determined."
            );
        }

        List<Assignment>? assignments = new();
        if (localDiagnostics.Count == 0)
        {
            assignments = ParseAssignments(
                yamlMappingNode: yamlMappingNode,
                logicalPath: logicalPath,
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                parameterDictionary: parameterDictionary,
                instanceDictionary: instanceDictionary,
                instanceClass: instanceClass!
            );
        }

        List<Element>? elements = new();
        if (localDiagnostics.Count == 0)
        {
            elements = ParseElements(
                yamlMappingNode: yamlMappingNode,
                logicalPath: logicalPath,
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                instanceClass: instanceClass!
            );
        }

        if (assignments.Count > 0 && elements.Count > 0)
        {
            location = new Location(yamlMappingNode, logicalPath);
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InstanceBothAssignmentsAndElementsSet,
                message: $"Named instance '{instanceName}' in {logicalPath} has both " +
                         "assignments and elements.",
                location: location
            );
        }

        incomingDiagnostics.AddRange(localDiagnostics);

        var instance = new Instance(
            instanceName: instanceName,
            @class: instanceClass,
            classToken: classToken,
            instanceIsArray: instanceIsArray,
            assignments: assignments,
            elements: elements,
            location: new Location(yamlMappingNode, logicalPath),
            diagnostics: localDiagnostics
        );

        if (!instanceName.IsWhiteSpace())
        {
            if (instanceDictionary.ContainsKey(instanceName!))            
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.InstanceDuplicateName,
                    message: $"Named instance '{instanceName}' is defined more than once.",
                    location: location
                );
            else
                instanceDictionary.Add(instanceName!, instance);
        }
 
        return instance;
    }

    List<Assignment> ParseAssignments(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        Dictionary<string, Parameter> parameterDictionary,
        Dictionary<string, Instance> instanceDictionary,
        Class instanceClass
    )
    {
        var tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceAssignments];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var list = new List<Assignment>();
        var yamlSequenceNode = GetChildSequence(yamlMappingNode, tokenTypeName);
        if (yamlSequenceNode is null) return list;

        for (int i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var childLogicalPath = $"{logicalPath}.{tokenTypeName}[{i}]";
            var childNode = yamlSequenceNode.Children[i];
            var childYamlMappingNode = childNode as YamlMappingNode;
            if (childYamlMappingNode is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.AssignmentInvalidNode,
                    message: $"Assignment at {childLogicalPath} must be a mapping node.",
                    location: new Location(childNode, childLogicalPath)
                );
                continue;
            }

            var dto = ParseAssignment(
                yamlMappingNode: childYamlMappingNode, 
                logicalPath: childLogicalPath, 
                tokenPath: tokenPath,
                incomingDiagnostics: localDiagnostics,
                parameterDictionary: parameterDictionary,
                instanceDictionary: instanceDictionary,
                instanceClass: instanceClass
            );

            if (dto is not null) list.Add(dto);
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return list;
    }
    
    private Assignment ParseAssignment(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        Dictionary<string, Parameter> parameterDictionary,
        Dictionary<string, Instance> instanceDictionary,
        Class instanceClass
    )
    {
        var type = typeof(Assignment);        
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();
        localDiagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode, 
                dtoType: type, 
                logicalPath: logicalPath
            )
        );

        // Assignment/Parameter Name
        tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceAssignmentName];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var parameterName = GetScalar(yamlMappingNode, tokenTypeName);
        bool haveParameterName = !parameterName.IsWhiteSpace();
        if (parameterName.IsWhiteSpace())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.AssignmentMissingParameterName,
                message: $"Missing 'parameterName' in {logicalPath}.",
                location: location
            );
        }
        else if (!parameterName.IsValidIdentifier())
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.InvalidIdentifier,
                message: $"Assignment parameterName '{parameterName}' must be a simple " +
                         "identifier (no namespace).",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{parameterName}']" }
            );
        }

        var instanceClassQualified = instanceClass.ClassQualified!;
        var parameter = parameterDictionary.GetValueOrDefault($"{instanceClassQualified}.{parameterName}");
        var foundParameter = parameter is not null;

        if (haveParameterName && !foundParameter)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.AssignmentParameterNotFound,
                message: $"Assignment parameterName '{parameterName}' not found in class '{instanceClassQualified}'.",
                location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{parameterName}']" }
            );
        }

        // Literal
        tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceAssignmentLiteral];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var assignmentLiteral = GetScalar(yamlMappingNode, tokenTypeName);
        var haveAssignmentLiteral = assignmentLiteral is not null;
        var assignmentLiteralInferredClass = haveAssignmentLiteral ? assignmentLiteral.InferredClass() : null;
        if (parameter is not null && assignmentLiteralInferredClass is not null)
        {
            if (parameter.Class != @"String" &&  parameter.Class != assignmentLiteralInferredClass)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.AssignmentLiteralTypeMismatch,
                    message: $"Assignment literal '{assignmentLiteral}' inferred type '{assignmentLiteralInferredClass}' " +
                             $"does not match parameter '{parameterName}' type '{parameter.Class}'.",
                    location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{assignmentLiteral}']" }
                );
            }
        }
        
        // Parameter Instance
        tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceAssignmentInstance];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var parameterInstanceName = GetScalar(yamlMappingNode, tokenTypeName);
        var haveParameterInstanceName = false;

        if (!parameterInstanceName.IsWhiteSpace())
        {
            haveParameterInstanceName = true;
            if (!parameterInstanceName.IsValidIdentifier())
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.InvalidIdentifier,
                    message: $"NamedInstanceName '{parameterInstanceName}' is not a valid identifier.",
                    location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{parameterInstanceName}']" }
                );
            }
            else if (!parameterInstanceName.IsPascalCase())
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.IdentifierNotPascalCase,
                    message: $"NamedInstanceName '{parameterInstanceName}' is not in PascalCase.",
                    location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{parameterInstanceName}']" }
                );
            }
        }
        
        if (!haveAssignmentLiteral && !haveParameterInstanceName)
        {
            if (foundParameter && !parameter!.IsNullable)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.AssignmentNoValueOrInstance,
                    message: $"Assignment at {logicalPath} must specify either assignedValue or assignedNamedInstance.",
                    location: location
                );
            }
        }
        else if (haveAssignmentLiteral && haveParameterInstanceName)
        {
            localDiagnostics.Add(
                diagnosticCode: DiagnosticCode.AssignmentBothValueAndInstance,
                message: $"Assignment at {logicalPath} cannot specify both assignedValue and assignedNamedInstance.",
                location: location
            );
        }

        // Initializer Parameter Assignment Clause
        string? initializerParameterAssignmentClause = null;
        if (localDiagnostics.Count == 0)
        {
            if (haveAssignmentLiteral)
                if (!parameter!.IsArray)
                {
                    if (parameter.ClassQualified!.Equals("System.String", StringComparison.OrdinalIgnoreCase)) 
                        assignmentLiteral = $"\"{assignmentLiteral}\"";
                    initializerParameterAssignmentClause = $"{parameterName}: {assignmentLiteral}";
                }
                else
                    if (assignmentLiteral == @"[]")
                        initializerParameterAssignmentClause = $"{parameterName}: Array.Empty<{parameter.ClassQualified}>()";
                    else
                        localDiagnostics.Add(
                            diagnosticCode: DiagnosticCode.AssignmentLiteralArrayNotSupported,
                            message: $"Array parameter '{parameterName}' at {logicalPath} does not support literal assignments. " +
                                    "Use instance assignment for arrays.",
                            location: location
                        );
            else if (haveParameterInstanceName)
                initializerParameterAssignmentClause = parameter!.IsArray
                    // ToDo: Kludge: More generation logic where it doesn't belong. Need to refactor later.
                    ? $"{parameterName}: registry.Get{parameterInstanceName}_Internal()"
                    : $"{parameterName}: registry.Get{parameterInstanceName}()";
            else
                // No literal or instance specified
                if (parameter!.IsNullable)
                    initializerParameterAssignmentClause = $"{parameterName}: null";
                else
                    localDiagnostics.Add(
                        diagnosticCode: DiagnosticCode.AssignmentNoValueOrInstance,
                        message: $"Non nullable parameter null Assignment at {logicalPath} must specify either assignedValue or assignedNamedInstance.",
                        location: location
                    );
        }

        incomingDiagnostics.AddRange(localDiagnostics);

        if (parameter is null)
        {
            throw new InvalidOperationException(
                $"Cannot continue parsing assignment for parameter '{parameterName}' " +
                "because the parameter could not be found on the class {}."
            );
        }
        return new Assignment(
            name: parameterName,
            literal: assignmentLiteral,
            literalInferredClass: assignmentLiteralInferredClass,
            instance: parameterInstanceName,
            initializerParameterAssignmentClause: initializerParameterAssignmentClause,
            parameter: parameter!,
            location: location,
            diagnostics: localDiagnostics
        );
    }

    List<Element> ParseElements(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        string tokenPath,
        List<Diagnostic> incomingDiagnostics,
        Class instanceClass
    )
    {
        var type = typeof(Element);
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var localDiagnostics = new List<Diagnostic>();

        var list = new List<Element>();
        var yamlSequenceNode = GetChildSequence(yamlMappingNode, tokenTypeName);
        if (yamlSequenceNode is null) return list;

        for (int i = 0; i < yamlSequenceNode.Children.Count; i++)
        {
            var childLogicalPath = $"{logicalPath}.{tokenTypeName}[{i}]";
            var childNode = yamlSequenceNode.Children[i];
            var childYamlMappingNode = childNode as YamlMappingNode;
            if (childYamlMappingNode is null)
            {
                localDiagnostics.Add(
                    diagnosticCode: DiagnosticCode.ElementInvalidNode,
                    message: $"Element at {childLogicalPath} must be a mapping node.",
                    location: new Location(childNode, childLogicalPath)
                );
                continue;
            }

            var dto = ParseElement(
                yamlMappingNode: childYamlMappingNode,
                logicalPath: childLogicalPath,
                incomingDiagnostics: localDiagnostics,
                instanceClass: instanceClass
            );

            if (dto is not null) list.Add(dto);
        }

        incomingDiagnostics.AddRange(localDiagnostics);
        return list;
    }
    Element ParseElement(
        YamlMappingNode yamlMappingNode,
        string logicalPath,
        List<Diagnostic> incomingDiagnostics,
        Class instanceClass
    )
    {
        var type = typeof(Element);        
        var tokenTypeName = TypeToTokenName[type];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        var location = new Location(yamlMappingNode, logicalPath);
        var diagnostics = new List<Diagnostic>();
        diagnostics.AddRange(
            ValidateMappingKeys(
                yamlMappingNode: yamlMappingNode, 
                dtoType: type, 
                logicalPath: logicalPath
            )
        );

        tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceElementLiteral];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var literal = GetScalar(yamlMappingNode, tokenTypeName);
        var haveLiteral = !literal.IsWhiteSpace();;

        tokenTypeName = TokenTypeToName[TokenTypes.instancesInstanceElementInstance];
        logicalPath = $"{logicalPath}/{tokenTypeName}/";
        location = new Location(yamlMappingNode, logicalPath);
        var instance = GetScalar(yamlMappingNode, tokenTypeName);
        var haveInstance = false;
        if (!instance.IsWhiteSpace())
        {
            if (!instance.IsValidIdentifier())
            {
                diagnostics.Add(
                    diagnosticCode: DiagnosticCode.InvalidIdentifier,
                    message: $"NamedInstanceName '{instance}' is not a valid identifier.",
                    location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{instance}']" }
                );
            }
            else if (!instance.IsPascalCase())
            {
                diagnostics.Add(
                    diagnosticCode: DiagnosticCode.IdentifierNotPascalCase,
                    message: $"NamedInstanceName '{instance}' is not in PascalCase.",
                    location: location with { LogicalPath = $"{logicalPath}[@{tokenTypeName}='{instance}']" }
                );
            }
            else
                haveInstance = true;
        }

        if (!haveLiteral && !haveInstance) 
        {
            diagnostics.Add(
                diagnosticCode: DiagnosticCode.ElementMissingValue,
                message: $"Element at {logicalPath} must specify either assignedValue or assignedNamedInstance.",
                location: location
            );
        }
        else if (haveLiteral && haveInstance)
        {
            diagnostics.Add(
                diagnosticCode: DiagnosticCode.ElementBothValueAndInstance,
                message: $"Element at {logicalPath} cannot specify both assignedValue and assignedNamedInstance.",
                location: location
            );
        }

        incomingDiagnostics.AddRange(diagnostics);

        return new Element(
            literal: literal,
            instance: instance,
            instanceClass: instanceClass,
            location: location,
            diagnostics: diagnostics
        );
    }

    static (
        Dictionary<string, Namespace> NamespaceDictionary,
        Dictionary<string, Class> ClassDictionary,
        Dictionary<string, Interface> InterfaceDictionary,
        Dictionary<string, Parameter> ParameterDictionary
    ) BuildDictionaries(
        IReadOnlyList<Namespace> namespaces,
        List<Diagnostic> diagnostics
    )
    {
        Dictionary<string, Namespace> namespaceDictionary = new();
        Dictionary<string, Class> classDictionary = new(); 
        Dictionary<string, Interface> interfaceDictionary = new();
        Dictionary<string, Parameter> parameterDictionary = new();

        if (diagnostics.Count > 0)
        {
            return (
                namespaceDictionary,
                classDictionary,
                interfaceDictionary,
                parameterDictionary
            );
        }

        foreach (var @namespace in namespaces)
        {
            if (namespaceDictionary.ContainsKey(@namespace.NamespaceName))
            {
                diagnostics.Add(
                    diagnosticCode: DiagnosticCode.DuplicateNamespace,
                    message: $"Duplicate namespace '{@namespace.NamespaceName}' found.",
                    location: @namespace.Location
                );
                continue;
            }
            else
                namespaceDictionary.Add(@namespace.NamespaceName, @namespace);

            foreach (var @class in @namespace.Classes)
            {                
                if (classDictionary.ContainsKey(@class.ClassQualified!))
                {
                    diagnostics.Add(
                        diagnosticCode: DiagnosticCode.DuplicateClass,
                        message: $"Duplicate class '{@class.ClassQualified}' found.",
                        location: @class.Location
                    );
                    continue;
                }
                else
                    classDictionary.Add(@class.ClassQualified!, @class);

                foreach (var parameter in @class.Parameters)
                {
                    var parameterKey = $"{@class.ClassQualified}.{parameter.Value.ParameterName}";
                    if (parameterDictionary.ContainsKey(parameterKey))
                        diagnostics.Add(
                            diagnosticCode: DiagnosticCode.DuplicateParameter,
                            message: $"Duplicate parameter '{parameterKey}' found.",
                            location: parameter.Value.Location
                        );
                        else
                            parameterDictionary.Add(parameterKey, parameter.Value);
                }
            }

            foreach (var @interface in @namespace.Interfaces)
            {
                if (interfaceDictionary.ContainsKey(@interface.InterfaceQualified!))
                {
                    diagnostics.Add(
                        diagnosticCode: DiagnosticCode.DuplicateInterface,
                        message: $"Duplicate interface '{@interface.InterfaceQualified}' found.",
                        location: @interface.Location
                    );
                    continue;
                }
                else
                    interfaceDictionary.Add(@interface.InterfaceQualified!, @interface);
            }
        }
        return (
            namespaceDictionary,
            classDictionary,
            interfaceDictionary,
            parameterDictionary
        );
    }
}
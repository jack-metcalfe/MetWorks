📑 Code Generation Template Matrix
Template	Purpose	Placeholders	Composed Into	Generated Pattern Shape
Initializer.Invoker	Per‑initializer invoker snippet for a named instance	SafeKey, Provenance, Namespace	Initializer	Internal static partial class with Invoke_{{SafeKey}}Async(IServiceProvider) returning Task.CompletedTask
Initializer	Aggregate runner for all initializers	GeneratedHeader, Usings, Members, Namespace	Top‑level initializer entrypoint	Public static class GeneratedInitializers with RunAllInitializersAsync(IServiceProvider) calling invokers
NamedInstanceAccessor.Class	Static class exposing accessor functions	ClassName, FunctionSnippets, Namespace	Accessor functions	Public static class with multiple accessor function snippets
NamedInstanceAccessor.Function.Initializer	Initializer‑mode accessor for a named instance	SafeKey, Provenance, Namespace	Accessor class	Internal static partial class with async Get_{{SafeKey}}_Async(IServiceProvider) returning object?
NamedInstanceAccessor.Function.NamedInstanceArray	Accessor for arrays of named instances	SafeKey, Provenance, Namespace	Accessor class	Internal static partial class with GetArray_{{SafeKey}}(IServiceProvider) returning IReadOnlyList
NamedInstanceAccessor.Function	Accessor function for a single named instance	SafeKey, Provenance, Namespace	Accessor class	Internal static partial class with sync Get_{{SafeKey}}(IServiceProvider) returning object?
PrimitiveArray.InnerMembers	Container for primitive array inner members	Members	PrimitiveArray	Snippet block of member lines
PrimitiveArray.Member	Per‑element add lines for primitive array	SafeKey, Provenance, ElementType, FailFast	PrimitiveArray.InnerMembers	Internal static partial class with element‑specific member snippet
PrimitiveArray	Aggregate primitive array class	GeneratedHeader, Usings, Namespace, SafeKey, ArrayComment, Members	Top‑level primitive array entrypoint	Public static partial class {{SafeKey}}_PrimitiveArrays with members
Registration.Fragment	Per‑registration fragment for a named instance	SafeKey, Provenance, ServiceType, ImplementationType, Lifetime, FailFast, Namespace	Registration	Internal static partial class with Register_{{SafeKey}}(IServiceCollection)
Registration	Aggregate service registration entrypoint	GeneratedHeader, Usings, Namespace, Members	Top‑level registration entrypoint	Public static class GeneratedRegistrations with AddGeneratedServices(IServiceCollection)
Registry.Member	Per‑registry member fragment	SafeKey, Provenance, FailFast, MembersFragment, Namespace	Registry	Internal static partial class with registry‑specific members
Registry	Aggregate registry entrypoint	GeneratedHeader, Usings, Namespace, Members	Top‑level registry entrypoint	Public static partial class RegistryAggregate with composed member partials
🧩 How to Use This Matrix
Purpose: clarifies why the template exists.

Placeholders: defines the token contract each template expects.

Composed Into: shows nesting relationships (e.g., Initializer.Invoker → Initializer).

Generated Pattern Shape: gives a quick mental model of the emitted code.
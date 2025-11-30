Overview
This document defines the authoritative input format for DDI YAML configuration files. It is intentionally strict and deterministic: keys are case sensitive, generics are forbidden, and any deviation produces a provenance‑linked diagnostic. Use this guide to author valid configuration files and to run local validation.

File Structure and Top Level Keys
Root layout
Top-level key: Namespaces is the primary container for type declarations.

Other top-level keys: CodeGen, Assemblies, NamedInstances, SourcePath, and Provenance are allowed when required by your workflow. Unknown top-level keys are rejected.

Casing and exact keys
Keys are case sensitive. Use the exact names shown in examples.

Unknown keys at any object level are treated as errors. The schema enforces additionalProperties: false for strict sections.

Recommended file extension
Use .yaml or .yml. Keep encoding UTF‑8 without BOM.

Types and Naming Rules
Type vs FullName semantics
Type is a plain, namespace‑qualified type identifier. Examples: MyCompany.Product.SomeType or GlobalType for global namespace.

Must not contain assembly qualifiers such as Version=, Culture=, or PublicKeyToken=.

Must not use generic notation such as backtick `, angle brackets < >, or any other generic syntax. Generics are forbidden in input.

FullName is reserved exclusively for assembly full names in the standard .NET format, for example: MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null.

Namespace and simple name splitting
The namespace is everything before the last dot . in the Type value. The simple type name is the segment after the last dot.

If there is no dot, the type is considered to be in the global namespace and the simple name is the entire Type string.

Assemblies
Assembly may be a short assembly name (e.g., MyAssembly) or omitted.

If you provide an assembly full name, use the FullName field in the Assemblies section and validate it with AssemblyName semantics.

Forbidden constructs
Generic fields such as GenericArity and GenericParameterNames are not allowed.

Generic notation in Type strings (e.g., MyType`1, MyType<T>) is an error.

Type strings containing assembly tokens are an error; use FullName for assembly metadata.

Provenance and Diagnostics
Provenance model
Every raw input element may include provenance metadata: SourcePath, LineZeroBased, ColumnZeroBased, and LogicalPath. This metadata is used to produce precise diagnostics.

The normalizer maps raw provenance into canonical provenance so every diagnostic can point to the exact location in the source YAML.

Diagnostic behavior
The normalizer is strict and deterministic. It never guesses. If a rule is violated, the normalizer emits a diagnostic with:

Severity: Error, Warning, or Info.

Message: explicit, actionable text.

Origin: provenance pointing to the YAML location.

FailFast is configurable. By default the normalizer accumulates diagnostics so users see all issues in one pass.

Validation and Schema
JSON Schema
A JSON Schema is provided in schema/configuration.schema.json. It enforces:

Exact key names and casing for strict sections.

Forbidden generic keys via additionalProperties: false at the Types object level.

Basic types for Type, FullName, and Assembly.

Local validation commands
Validate YAML against schema (example using ajv-cli and yamljs):

bash
npm install -g ajv-cli yamljs
yaml2json path/to/config.yaml | ajv validate -s schema/configuration.schema.json -d -
Run unit tests:

bash
dotnet test
Runtime checks
The normalizer performs additional checks that are not expressible in JSON Schema, including:

Rejecting Type strings that contain assembly tokens.

Rejecting generic notation in Type.

Validating FullName strings using AssemblyName parsing.

Examples and Canonical Errors
Valid example
yaml
Namespaces:
  - Namespace: MyCompany.Product
    Types:
      - Type: MyCompany.Product.Person
Assemblies:
  - Assembly: MyAssembly
    FullName: MyAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
Invalid examples and expected diagnostics
Generic key present

yaml
Types:
  - Type: MyCompany.Product.List
    GenericArity: 1
Diagnostic: Error: GenericArity is not supported; generics are disallowed in input. Origin: file.yaml:12:3.

Generic notation in Type

yaml
Types:
  - Type: MyCompany.Product.List`1
Diagnostic: Error: Type 'MyCompany.Product.List\1' appears to use generic notation; generics are disallowed in input. Origin: file.yaml:8:5.`

Assembly tokens in Type

yaml
Types:
  - Type: MyCompany.Product.Foo, Version=1.0.0.0
Diagnostic: Error: Type 'MyCompany.Product.Foo, Version=1.0.0.0' contains assembly qualifiers; use FullName for assembly full names and Type for plain type names. Origin: file.yaml:9:3.

Migration and Future Changes
If generics are required later, the change will be explicit and versioned. The normalizer will be extended to accept explicit generic fields and the schema will be updated. Existing inputs will continue to be rejected until they are migrated to the new format.

Document any format version changes in docs/format.md and bump the Provenance version to indicate the new rules.

Where to find files
Schema: schema/configuration.schema.json

Docs: docs/format.md and docs/errors.md

Normalizer: src/.../Normalizer.cs

Tests: tests/.../TypeNormalizationTests.cs
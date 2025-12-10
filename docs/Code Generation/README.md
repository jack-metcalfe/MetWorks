# DdiCodeGen.SourceDto

This assembly contains all canonical Data Transfer Objects (DTOs) used across MetWorks solutions.

## Principles
- **Single source of truth**: DTOs are defined here only.
- **Minimal dependencies**: DTOs are plain C# classes, no external packages.
- **Consistency**: All projects use lowercase booleans, explicit nullable and langversion settings.
- **Opt‑in references**: Other assemblies add explicit references to this project when needed.

## Usage
To reference this assembly from another project:

```bash
dotnet add <YourProject>.csproj reference ../DdiCodeGen.SourceDto/DdiCodeGen.SourceDto.csproj

Why Constructors Are Excluded
This DTO model intentionally does not represent constructors. Instead, all dependency injection and setup logic is modeled through async initializers.

Rationale
Minimal construction: Constructors should be empty or limited to trivial primitive parameters. Complex setup belongs in initializers.

Debugging clarity: Bugs triggered during construction are harder to diagnose, especially in layered systems.

Async support: Constructors cannot be async, while initializers can. Async initializers provide a supported path for dependency injection.

Consistency with YAML: The input schema describes initializers, not constructors. DTOs mirror that design.

Supported path
Parameterless constructors are assumed.

All setup parameters are modeled as InitializerDto with async support.

Contributors should not expect constructor metadata in the DTO layer.
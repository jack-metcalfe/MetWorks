Author guidance (short snippet for README)
Non‑primitive parameters must be interfaces. Use qualifiedInterfaceName for any parameter whose type is not a primitive (string, int, bool, etc.).

Named instances are explicit wiring. For any interface parameter, assign a named instance that exposes the same interface (the named instance must declare qualifiedInterfaceName equal to the parameter interface).

Order matters. Named instances must be declared before they are referenced.

Arrays and nullables. Use [] for arrays and ? for nullable value types; the transformer will parse these into IsArray/IsNullable and validate assignments accordingly.

Example (YAML fragment + expected validation)
YAML (valid):

yaml
classes:
  - className: "SettingsRepository"
    initializerParameters:
      - parameterName: "iFileLogger"
        qualifiedInterfaceName: "InterfaceDefinition.IFileLogger"
namedInstances:
  - namedInstanceName: "TheFileLogger"
    qualifiedClassName: "Logging.FileLogger"
    qualifiedInterfaceName: "InterfaceDefinition.IFileLogger"
    assignments: []
Valid: parameter expects IFileLogger, named instance TheFileLogger exposes IFileLogger.

YAML (invalid):

yaml
classes:
  - className: "SettingsRepository"
    initializerParameters:
      - parameterName: "settingConfigurations"
        qualifiedClassName: "Settings.SettingConfiguration"   # non-primitive concrete class
Invalid: non‑primitive parameter declared as concrete class → Error: must be an interface.
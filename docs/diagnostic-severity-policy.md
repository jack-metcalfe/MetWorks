Diagnostic Severity Policy
Purpose
Diagnostics communicate structural and semantic issues found during YAML parsing. Severity levels determine whether code generation can proceed, whether warnings must be addressed, or whether information is simply annotated for maintainers.

Severity Levels
Error
Definition: A violation of grammar or required structure that prevents deterministic parsing or valid code generation.

Examples:

Missing required fields (namespaceName, className, namedInstanceName).

Invalid identifiers (Ns.Type?[] in unsupported form).

Both assignments and elements set in a named instance.

Action:

Code generation must halt.

Contributor must fix the YAML before proceeding.

Errors are non‑negotiable; they indicate invalid input.

Warning
Definition: A violation of best practices or optional fields that does not block parsing but may cause degraded output or ignored sections.

Examples:

Empty or malformed packageReferences entries.

Namespace segments that parse but don’t follow naming conventions.

Action:

Code generation may proceed.

Contributor should fix warnings to avoid surprises.

Warnings are logged and surfaced prominently.

Info
Definition: Non‑blocking annotations that provide context or traceability but do not indicate invalid input.

Examples:

Ignored optional fields.

Provenance notes for skipped nodes.

Action:

No fix required.

Info diagnostics serve as breadcrumbs for maintainers.

Useful for debugging and onboarding.

Contributor Guidance
Always fix Errors before committing YAML.

Treat Warnings as TODOs. They won’t block generation but should be cleaned up.

Use Info diagnostics for context only. They are not actionable but help future maintainers understand provenance.

Consistency matters. Every parser emits diagnostics with the same severity rules, so contributors don’t have to guess.

This policy ensures that diagnostics are predictable, actionable, and teachable. It reduces cognitive load for maintainers and keeps your loader aligned with the principles from Software Engineering at Google — clarity, consistency, and readability.

Would you like me to now create a visual severity matrix (a chart mapping parser → diagnostic → severity) so contributors can see the whole landscape at a glance?
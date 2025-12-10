# Pipeline Walkthrough Checklist
*Date: 2025‑12‑03*

This checklist tracks immediate actions and test coverage as we step through the pipeline in runtime order. Each item should be completed before moving to the next stage.

---

## Parse Stage (`DdiCodeGen.SourceDto.Parser`)
- [ ] Open `SourceDtoParser.cs` and confirm provenance stack entry includes parser stage + source path.
- [ ] Verify malformed YAML produces diagnostics, not null DTOs.
- [ ] Add/strengthen parser tests:
  - [ ] Happy path fixture parses correctly.
  - [ ] Missing fields fixture produces diagnostics.
  - [ ] Malformed YAML fixture throws/diagnoses as expected.

---

## Transform Stage (`DdiCodeGen.SourceDto`)
- [ ] Open `RawToCanonicalTransformer.cs` and implement `ReturnType = QualifiedInterfaceName ?? QualifiedClassName`.
- [ ] Enforce parameter mutual exclusivity (`QualifiedClassName` vs `QualifiedInterfaceName`).
- [ ] Append transformer provenance entry.
- [ ] Add/strengthen transformer tests:
  - [ ] ReturnType set correctly for exposed vs internal instances.
  - [ ] Illegal combos rejected with diagnostics.
  - [ ] Provenance preserved across transformation.

---

## Validation (`Validation.Exchange`, `Core.Diagnostics`)
- [ ] Confirm diagnostic codes/messages are stable and machine‑parseable.
- [ ] Ensure provenance append semantics consistent across parser and transformer.
- [ ] Add unit tests for each validation rule (pass + fail cases).

---

## Generation (`DdiCodeGen.Generator`, `DdiCodeGen.Templates.StaticDataStore`)
- [ ] Update `Accessor.template` to use only `{{ReturnType}}`, `{{QualifiedClassName}}`, `{{NamedInstanceName}}`.
- [ ] Implement deterministic substitution helper (`TemplateRenderer.Render`).
- [ ] Implement `CodeEscaping.EscapeTypeKey` for string literals.
- [ ] Add generator tests:
  - [ ] Integration test parser → transformer → generator produces expected scaffolding.
  - [ ] Snapshot tests for golden fixtures.
  - [ ] Optional Roslyn compile test for critical fixtures.

---

## Support Libraries (`Core.Models`, helpers)
- [ ] Ensure canonical DTOs expose generator‑needed fields (`ReturnType`, `QualifiedClassName`, `NamedInstanceName`, `Namespace`, `RegistryClassName`).
- [ ] Add unit tests for helpers (`EscapeTypeKey`, fixture loaders).

---

## Integration & CI
- [ ] Run parser tests only and fix failures.
- [ ] Run transformer tests next and fix failures.
- [ ] Run generator integration tests last.
- [ ] Ensure `dotnet test` passes across all projects.
- [ ] Configure CI to run parser/transformer tests on every PR; generator integration tests nightly.


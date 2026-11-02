# Contributing to WordsCat

Read `AGENTS.md`, `docs/PROJECT_STATE.md`, `docs/REQUIREMENTS.md` and `docs/DEVELOPMENT.md` before editing. Discuss substantial behavior changes in an issue first. Use English paths and preserve the native C#/.NET 10 Windows Forms stack, ReadDock compatibility identifiers, user data formats, notebooks and custom templates.

## Checks before a pull request

Run on Windows 11 with the .NET 10 SDK:

```powershell
./build.ps1
./verify.ps1
./scripts/check-secrets.ps1
./scripts/check-source.ps1
```

UI changes also require `./verify.ps1 -IncludeUi` in an interactive desktop and visual inspection. CI runs the non-UI suite; a passing CI job does not replace actual-window checks. Use synthetic documents and isolated test directories. Do not modify user Data or the exact bytes of `fixtures/vocabulary-v0.1.json`.

Never put real credentials in code, documentation, tests, fixtures, logs or screenshots. Never read production keys during development/tests. Use the existing fixed fake credential and local mock handlers. Keep Data, runtime settings, private captures and build/test/release outputs out of Git. Enable the pre-commit guard with `git config core.hooksPath .githooks`.

## AI-assisted contributions

Contributors remain responsible for all submitted changes. Disclose material AI assistance in the pull request, explain the resulting behavior, and identify the checks actually run. You must run `verify.ps1` and personally review the complete diff before submission, including generated code, workflows, dependency changes, license notices and documentation. Do not claim a model-generated test result as an executed result. Add meaningful regressions for bugs; use UI checks for window behavior. Do not send credentials, private captures or notebooks to an AI service.

Keep pull requests focused. Describe the problem, change, validation, and remaining limitations. Update the changelog/project state and preserve third-party licenses. Contributions are submitted under the repository's Apache-2.0 license; do not add material you cannot license compatibly. Report security issues privately as described in `SECURITY.md`.

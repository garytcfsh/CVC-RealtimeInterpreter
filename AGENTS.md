# Repository Guidelines

## Project Structure & Module Organization

Application code lives under `src/`, with the WPF Desktop project referencing the portable/Windows Core library. Automated Core tests live under `tests/`; product and architecture documentation lives under `docs/`. Organize features by domain within these projects. Generated outputs belong under ignored `artifacts/`, separated by project and build variant.

Place configuration at the repository root and document any new top-level directory in the README. Do not commit generated output, dependency caches, credentials, or local recordings.

## Build, Test, and Development Commands

Use the .NET 10 SDK selected by `global.json`. Run commands from the repository root:

- `dotnet build CVC.RealtimeInterpreter.slnx`: restore and build all projects.
- `dotnet test tests/CVC.RealtimeInterpreter.Core.Tests`: run the portable Core tests.
- `dotnet run --project src/CVC.RealtimeInterpreter.Desktop`: launch the Windows preview.
- `dotnet format CVC.RealtimeInterpreter.slnx --verify-no-changes --no-restore`: check C# formatting.

See `README.md` for restore/publish commands and current implementation limitations.

## Coding Style & Naming Conventions

Follow the formatter and linter selected for the implementation language; do not hand-format against their output. Use 2 spaces for JSON, YAML, and JavaScript/TypeScript configuration unless the chosen formatter specifies otherwise. Use `PascalCase` for classes and UI components, `camelCase` for functions and variables, and `kebab-case` for directories and non-code filenames. Name modules for their responsibility, such as `speech-recognizer` or `translation-session`.

Keep secrets in local environment files such as `.env`; commit only a sanitized `.env.example` with required variable names.

For the C#/.NET 10 WPF application, follow [docs/architecture.md](docs/architecture.md). Use four-space indentation in C# and XAML. C# uses Allman braces and mandatory braces for control-flow bodies, including single statements; place `if`, `else`, loops, and exception-handling clauses and their bodies on separate lines. Never compress these into single-line statements. Use PascalCase for C# methods, types, and public properties. In XAML, put each attribute on its own line rather than combining attributes on one line.

## Testing Guidelines

Add tests alongside each new behavior in `tests/` or the framework's standard location. Use descriptive names that state the expected outcome, e.g. `translates_audio_when_input_is_valid`. Include edge cases for audio permissions, network failures, and unsupported languages. Run the full suite before opening a pull request.

## Commit & Pull Request Guidelines

There is no commit history yet, so use concise imperative commit subjects: `Add streaming transcription client`. Keep commits focused and avoid mixing refactors with feature changes. Pull requests should explain the change, note testing performed, link relevant issues, and include screenshots or recordings for user-visible changes.

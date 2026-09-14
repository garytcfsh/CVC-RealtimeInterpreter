# Repository Guidelines

## Project Structure & Module Organization

This repository is currently a blank project scaffold. Keep application code under `src/`, automated tests under `tests/`, and static resources under `assets/` when those directories are introduced. Organize features by domain rather than placing unrelated modules in shared catch-all files. For example, use `src/audio/`, `src/translation/`, and `src/ui/` for a real-time interpreter application.

Place configuration at the repository root and document any new top-level directory in the README. Do not commit generated output, dependency caches, credentials, or local recordings.

## Build, Test, and Development Commands

No build, test, formatter, or package manifest is configured yet. When adding a runtime or build system, add its canonical commands to `README.md` and keep them reproducible from a clean checkout. Typical commands should cover:

- installing dependencies;
- running the application locally;
- linting and formatting;
- executing the complete test suite.

Prefer a single documented entry point per task (for example, `npm test` or `python -m pytest`) over undocumented one-off scripts.

## Coding Style & Naming Conventions

Follow the formatter and linter selected for the implementation language; do not hand-format against their output. Use 2 spaces for JSON, YAML, and JavaScript/TypeScript configuration unless the chosen formatter specifies otherwise. Use `PascalCase` for classes and UI components, `camelCase` for functions and variables, and `kebab-case` for directories and non-code filenames. Name modules for their responsibility, such as `speech-recognizer` or `translation-session`.

Keep secrets in local environment files such as `.env`; commit only a sanitized `.env.example` with required variable names.

## Testing Guidelines

Add tests alongside each new behavior in `tests/` or the framework's standard location. Use descriptive names that state the expected outcome, e.g. `translates_audio_when_input_is_valid`. Include edge cases for audio permissions, network failures, and unsupported languages. Run the full suite before opening a pull request.

## Commit & Pull Request Guidelines

There is no commit history yet, so use concise imperative commit subjects: `Add streaming transcription client`. Keep commits focused and avoid mixing refactors with feature changes. Pull requests should explain the change, note testing performed, link relevant issues, and include screenshots or recordings for user-visible changes.

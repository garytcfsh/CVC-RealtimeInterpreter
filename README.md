# CVC-RealtimeInterpreter

Windows 11 assistant for church interpreters, built with C#, .NET 10, and WPF.

## Current milestone

The foundation provides two production projects, an English/Traditional Chinese UI shell, sentence-aligned sample text, theme selection, shared text sizing, and automatically saved preferences.

**This is an interface preview, not a working interpretation service.** Start/Stop are intentionally disabled. Sample text is synthetic; there is no audio capture, AI request, speech playback, Scripture matching, transcript persistence/export, global shortcut, or configuration import/export yet. Selecting a voice or stale threshold currently saves a preference only. No API keys are requested or stored.

## Structure

- `src/CVC.RealtimeInterpreter.Desktop/`: WPF-UI views and CommunityToolkit.Mvvm ViewModels; startup composition.
- `src/CVC.RealtimeInterpreter.Core/`: portable settings, transcript models, AI contracts, and isolated Windows platform services.
- `tests/`: portable Core tests.
- `docs/`: [product discovery](docs/product-discovery.md) and [architecture](docs/architecture.md).
- `artifacts/`: ignored build, intermediate, and publish output separated by project and build variant.

Core targets portable .NET 10 and Windows 11 (.NET Windows SDK baseline 10.0.22000.0). Windows implementations are excluded from the portable build. The actual audio SDK baseline will be validated during audio integration.

## Development

Install the .NET 10 SDK selected by `global.json` and use Windows 11 for the desktop app. No mobile workloads are needed. Commands run from the repository root:

```powershell
dotnet restore CVC.RealtimeInterpreter.slnx
dotnet build CVC.RealtimeInterpreter.slnx
dotnet test tests/CVC.RealtimeInterpreter.Core.Tests
dotnet run --project src/CVC.RealtimeInterpreter.Desktop
dotnet format CVC.RealtimeInterpreter.slnx --verify-no-changes --no-restore
```

Build also restores dependencies automatically. Tests validate settings persistence and rejection of invalid/corrupt configuration. Formatting rules live in `.editorconfig`; review XAML for one attribute per line.

To publish a framework-dependent Windows x64 application:

```powershell
dotnet publish src/CVC.RealtimeInterpreter.Desktop -c Release -r win-x64 --self-contained false
```

Run the Desktop executable from its output or publish directory; its Core dependency is copied automatically.

## Settings

Preferences are saved to `%ProgramData%/CVC-RealtimeInterpreter/settings.json` when changed. The app displays a warning if permissions or storage prevent saving; do not run as administrator just to hide such failures. An installer/data-directory permission policy remains to be designed.

A missing file uses defaults without creating a file. An invalid file produces a warning and remains untouched on load; changing a preference subsequently writes a validated replacement. Settings are written through a temporary file and then replaced. No service data or credentials are part of this schema.

## Next integration work

Select and validate the AI provider; implement mixer/browser capture and device selection; then connect the session pipeline, playback policy, recovery, exports, and Scripture workflow. Preserve the service boundaries and coding style in `docs/architecture.md`.

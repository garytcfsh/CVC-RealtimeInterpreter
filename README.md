# CVC-RealtimeInterpreter

Windows 11 assistant for church interpreters, built with C#, .NET 10, and WPF.

## Current milestone

The foundation provides two production projects, a WPF-UI Gallery-style shell with collapsible left-side navigation, English/Traditional Chinese labels, sentence-aligned sample text, theme selection, shared text sizing, and automatically saved preferences. Home is the main page. Settings itself opens General (appearance, language, text size, storage) and toggles its group; AI is its only child (voice and stale-audio preferences). All pages retain their state. Buttons use WPF-UI controls; standard types such as ComboBox, RadioButton, and Slider use the library's styles.

**Audio capture is implemented; interpretation is not connected yet.** General settings lists Windows recording inputs and remembers the selected endpoint. Home Start/Stop controls that input through the Core capture service. Capture runs off the UI thread; packets are discarded without saving or uploading audio. Changing input stops capture. Device loss stops capture and preserves the selection; reconnecting requires an explicit Start. There is no automatic fallback to another microphone.

Home's input meter measures samples received by this app. After selecting the mixer input and pressing Start, speak into the mixer microphone: the bar should move with the signal and fall during silence. The status distinguishes **Receiving audio · signal detected**, **Receiving audio · silence**, and **No audio received**. Packet arrival expires after one second without data; Stop, input changes, and device loss clear the meter. A moving bar confirms signal, not speech intelligibility or translation quality. No audio is recorded or uploaded.

Sample text is synthetic and is cleared when Start is pressed. There are no AI requests, speech playback, Chrome capture, Scripture matching, transcript persistence/exports, global shortcuts, or configuration import/export yet. Stage 2 is unavailable. Voice and stale-threshold controls currently save preferences only. No API keys are requested or stored. The capture-only Start behavior is temporary; the interpretation pipeline will require valid provider credentials.

## Structure

- `src/CVC.RealtimeInterpreter.Desktop/`: WPF-UI views and CommunityToolkit.Mvvm ViewModels; startup composition.
- `src/CVC.RealtimeInterpreter.Core/`: portable settings, transcript models, AI contracts, and isolated Windows platform services.
- `tests/`: portable Core tests.
- `docs/`: [product discovery](docs/product-discovery.md) and [architecture](docs/architecture.md).
- `artifacts/`: ignored build, intermediate, and publish output separated by project and build variant.

Core targets portable .NET 10 and Windows 11 (.NET Windows SDK baseline 10.0.22000.0). Windows implementations and the pinned NAudio.Wasapi dependency are excluded from the portable build.

## Development

Install the .NET 10 SDK selected by `global.json` and use Windows 11 for the desktop app. No mobile workloads are needed. Commands run from the repository root:

```powershell
dotnet restore CVC.RealtimeInterpreter.slnx
dotnet build CVC.RealtimeInterpreter.slnx
dotnet test tests/CVC.RealtimeInterpreter.Core.Tests
dotnet run --project src/CVC.RealtimeInterpreter.Desktop
dotnet format CVC.RealtimeInterpreter.slnx --verify-no-changes --no-restore
```

Build also restores dependencies automatically. Tests validate settings persistence, invalid/corrupt configuration, device selection, disconnect/reconnect policy, permission failures, late callbacks, and Stop during startup. Formatting rules live in `.editorconfig`; review XAML for one attribute per line.

For hardware validation, connect the mixer through an appropriate Windows audio interface, select that recording input in Settings → General, then Start/Stop on Home. Allow desktop microphone access in Windows privacy settings if denied. Check that unplugging the interface stops capture and that reconnecting does not restart it. A mixer AUX connector and signal level still need verification; an AUX socket alone is not a Windows recording device.

A read-only developer check enumerates real Windows inputs without opening capture:

```powershell
dotnet run --project src/CVC.RealtimeInterpreter.Desktop -- --inspect-audio-inputs artifacts/audio-inputs.json
```

To publish a framework-dependent Windows x64 application:

```powershell
dotnet publish src/CVC.RealtimeInterpreter.Desktop -c Release -r win-x64 --self-contained false
```

Run the Desktop executable from its output or publish directory; its Core dependency is copied automatically.

## Direct visual previews

Generate PNGs of the actual WPF views without Computer Use or opening a visible window:

```powershell
dotnet run --project src/CVC.RealtimeInterpreter.Desktop -- --render-preview artifacts/preview
```

The renderer uses synthetic text, fake audio devices, and an in-memory settings store. It does not read or change user settings, capture real audio, or call AI services. It renders Light/Dark Home, General, and AI views at 900×620 and 1280×850, Traditional Chinese examples, collapsed-sidebar examples, and capturing/disconnected/access-denied states. An invisible WPF presentation source initializes normal control lifecycles before direct PNG rendering. Regression checks exercise navigation, language switching, preserved preferences/device selection, and fake capture controls across input changes and disconnects. PNGs stay under ignored `artifacts/`.

These previews check client layout and visual states; actual window dragging, resizing, minimize/maximize, and close behavior still require runtime checks. Normal startup fits the initial window dimensions to the Windows work area.

## Settings

Preferences are saved to `%ProgramData%/CVC-RealtimeInterpreter/settings.json` when changed. The app displays a warning if permissions or storage prevent saving; do not run as administrator just to hide such failures. An installer/data-directory permission policy remains to be designed.

A missing file uses defaults without creating a file. An invalid file produces a warning and remains untouched on load; changing a preference subsequently writes a validated replacement. Settings are written through a temporary file and then replaced. No service data or credentials are part of this schema.

## Next integration work

Validate mixer capture with the church's actual audio interface, select and validate the AI provider, then connect transcription and translation. Browser capture, output/playback policy, recovery, exports, and Scripture workflow follow. Preserve the service boundaries and coding style in `docs/architecture.md`.

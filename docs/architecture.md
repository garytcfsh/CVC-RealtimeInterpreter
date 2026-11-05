# Application Architecture

Status: agreed architectural direction; foundation implementation has started with user approval. See [README.md](../README.md) for implemented features and remaining integration work. Product behavior is recorded in [product-discovery.md](product-discovery.md).

## Technology and Project Boundaries

Use **C#**, **.NET 10**, and **WPF**, with two production projects. Test projects may be added separately under `tests/`.

| Project | Responsibilities | Dependencies |
| --- | --- | --- |
| `CVC.RealtimeInterpreter.Desktop` | WPF views, presentation ViewModels, localization, themes, navigation, dialogs, and startup composition | Core, WPF-UI, CommunityToolkit.Mvvm |
| `CVC.RealtimeInterpreter.Core` | Session orchestration, transcripts, AI adapters, audio services, Scripture matching, persistence, exports, settings, and platform abstractions | Framework-independent libraries; CommunityToolkit.Mvvm where useful |

Dependency direction is **Desktop → Core**. Core must never reference Desktop, WPF, WPF-UI, or presentation-specific types. CommunityToolkit.Mvvm is permitted in both projects, but permission to use it does not move screen-specific behavior into Core.

Desktop owns the composition root: it selects implementations and registers dependencies at startup. Use constructor injection for required collaborators. Avoid global service locators, static mutable session state, and a global messenger as the primary workflow mechanism.

## Proposed Directory Structure

Create directories when their responsibilities are implemented; this is not a requirement to create empty scaffolding.

```text
src/
  CVC.RealtimeInterpreter.Desktop/
    Views/
    ViewModels/
    Resources/
      Localization/
      Themes/
    Composition/
  CVC.RealtimeInterpreter.Core/
    Sessions/
    Transcripts/
    Audio/
    Translation/
    Speech/
    Scripture/
    Records/
    Configuration/
    Providers/
    Platforms/
      Windows/
tests/
docs/
```

Organize each feature around its own contracts, models, and implementation. Avoid generic `Helpers` or `Managers` folders that accumulate unrelated behavior. Keep modules within the two production projects until a concrete need justifies more assemblies.

## Build Output and Publishing

Centralize generated files under the repository's `artifacts/` directory, while keeping build outputs separate by project, configuration, target framework, and runtime where applicable.

- Configure the SDK's artifacts output layout through `UseArtifactsOutput` in a root `Directory.Build.props` when implementation starts.
- Keep each project's binaries under `artifacts/bin/<project>/<pivot>/` and intermediate files under `artifacts/obj/<project>/<pivot>/`. The SDK pivot distinguishes applicable configuration, framework, and runtime variants.
- Never point both projects, or Core's portable and Windows targets, at the same exact build or intermediate directory.
- Desktop references Core through `ProjectReference`; the build copies the required Core assembly and runtime dependencies into Desktop's output. Do not add manual merging or DLL-copy scripts for this relationship.
- Publish Desktop for distribution into one complete application directory under `artifacts/publish/`, including its required dependencies. Publish variants remain separated by configuration/framework/runtime as applicable.
- Exclude generated `artifacts/` contents from Git.
- Keep runtime user records and configuration under `ProgramData/CVC-RealtimeInterpreter`, independent of build and publish directories.

This policy centralizes output locations without sharing intermediate files or mixing incompatible build variants. It does not yet select an installer or deployment mechanism.

## Portable Core and Platform Implementations

Follow the reference repository's shared-library/platform concept without copying its unrelated build rules or dependencies.

- Build Core for portable `net10.0` and a Windows-specific .NET 10 target. Choose the exact Windows SDK/API baseline during audio validation; the supported product OS remains Windows 11.
- Explicitly exclude Windows source files and packages from the portable target. A `Platforms` folder in a normal class library does not automatically control compilation.
- Shared interfaces expose application concepts, not native handles, WPF objects, or provider SDK response types.
- Place Windows audio capture/playback, credential storage, storage-location resolution, and global-shortcut integration under `Platforms/Windows/`.
- Desktop supplies window lifecycle information where required. Window attachment and presentation threading remain in Desktop; native service details remain behind Core contracts.
- Add other platform implementations and build targets only when required. Do not require mobile workloads merely to build the first Windows release.
- Resolve ProgramData through the Windows platform implementation rather than embedding a Windows path in portable logic.

The portable Core target must build independently of the desktop UI. Future platforms can reuse shared processing, but will still require their own UI and platform adapters. Browser capture remains a feasibility item.

## Replaceable Service Boundaries

The following interface names are illustrative; finalize signatures during implementation.

| Boundary | Example contract | Responsibility |
| --- | --- | --- |
| Input | `IAudioCaptureSource` | Device/browser capture, audio frames, and source status |
| Output | `IAudioPlayback` | Selected-device playback, immediate stop, and disconnection status |
| Recognition | `ITranscriptionService` | Partial/final Mandarin segments and available confidence information |
| Translation | `ITranslationService` | English results associated with source segment IDs |
| Synthesis | `ISpeechSynthesisService` | Generate audio from finalized approved text and selected voice |
| Scripture | `IScriptureTextSource`, `IScriptureMatcher` | Resolve permitted texts and identify pre-entered readings |
| Records | `ISessionStore`, `ITranscriptExporter` | Recovery persistence and independent format exports |
| Platform | `ICredentialStore`, `IGlobalShortcutService`, `IStorageLocations` | OS-specific credentials, shortcuts, and directories |

One supported AI provider is sufficient initially, but recognition, translation, and synthesis remain separate contracts. Provider adapters translate SDK results/errors into application-owned models. Replacing a provider should not require rewriting views, records, or queue policy.

Use capability information where providers differ, especially for confidence and partial updates. Do not invent confidence values or treat recognition confidence as proof of translation accuracy.

## Session Ownership and Data Flow

Core owns the session lifecycle and coordinates:

1. Capture audio from the selected source.
2. Receive partial and finalized Mandarin text.
3. Translate segments and apply high-confidence Scripture substitution when appropriate.
4. Publish paired text/status updates to the presentation layer and persist finalized records.
5. In Stage 2, synthesize finalized English text and schedule playback.

Use stable session/segment IDs and revisions so delayed provider responses update the correct sentence. Partial and finalized state must be explicit. Translation failures remain associated with their Mandarin segments.

Represent capture, translation, playback, connection, and persistence status separately. A single session-state flag cannot express, for example, successful live translation while disk saving has failed.

Core enforces ordering, stale-content rules, mode selection, and cancellation. ViewModels invoke operations and present state; they must not implement a second playback queue or session policy.

## Concurrency, Playback, and Failure Isolation

- Keep capture, network processing, playback, and persistence off the UI thread. Desktop marshals presentation updates onto its dispatcher.
- Use explicit asynchronous lifetimes, cancellation, and bounded audio/speech queues. Specify overload behavior so slow generation cannot accumulate unlimited audio.
- Use a streaming provider connection with short-lived input buffers. Do not retain outage audio or save recordings.
- Mute stops local playback immediately and invalidates queued playback work. Late synthesis responses must not accidentally restart audio after mute, Stop, device replacement, or session changes.
- Resume selects the newest finalized sentence. Automatic stale skipping finishes the currently playing sentence before dropping stale queued content.
- Stop ends capture/playback immediately, then permits at most five seconds of finalization before saving completed segments.
- Isolate failures: storage errors must not stop interpretation; translation errors must not stop available Mandarin transcription; synthesis errors must not remove the bilingual display.
- Encode product-specific recovery rules explicitly. Connectivity can reconnect automatically, while exhausted credit and failed saving require user Retry; recovered audio requires explicit Resume.

Retain unsaved finalized text in memory according to product requirements. Bounded transport queues must not silently discard that text; report storage/memory limitations explicitly.

## Persistence, Exports, and Settings

- Use an internal recovery representation separate from Word, plain-text, and HTML outputs. The specific storage technology is not yet selected.
- Export each format from the same finalized transcript model. An individual export failure must not block other formats.
- Resolve the Windows data directory to `ProgramData/CVC-RealtimeInterpreter`; validate required permissions during implementation.
- Version settings and recovery schemas so later updates can migrate existing data.
- Save settings automatically using a strategy that avoids partially written configuration files.
- Store the API key through the credential abstraction. Exclude it from settings exports, recovery records, and diagnostic logs.
- Validate configuration imports before applying them, including device availability and shortcut conflicts.

## Coding Style

### C#

Use four-space indentation, nullable reference types, and Allman braces. Control-flow bodies always use braces, including single statements. Put `if`, `else`, `for`, `foreach`, `while`, `switch`, `try`, `catch`, and `finally` on separate lines from their bodies; do not compress statements onto one line.

```csharp
if (canResume)
{
    ResumePlayback();
}
else
{
    ShowUnavailableState();
}
```

Use PascalCase for types, methods, and public properties; camelCase for parameters and local variables; and `Async` suffixes for asynchronous methods. Prefer explicit service dependencies and typed errors over hidden global state.

### XAML

Use four-space indentation and **one attribute per line**, including short elements. Keep the element name on its own opening line when attributes are present. Avoid long inline bindings; expand complex bindings into property elements when that improves readability.

```xml
<Button
    Content="Resume AI"
    Command="{Binding ResumeCommand}"
    Margin="8,0,0,0" />
```

Keep interaction rules in ViewModels/Core. Code-behind may handle view-only concerns, such as scrolling, focus, and window integration. Bind semantic status to theme resources rather than storing WPF brushes in Core.

Pin stable package versions. Record formatting rules in tooling configuration when implementation starts; review XAML attribute layout explicitly if tooling cannot enforce it.

## Verification and Outstanding Technical Choices

Test Core independently using fake audio/provider/platform services. Prioritize sentence identity/order, mute/resume races, stale queues, partial failures, Stop timeout, recovery, and independent exports. Use controlled clocks for timing tests. Add Windows integration checks for actual capture, device loss, and shortcuts.

Provider selection, browser capture mechanics, Scripture access, recovery storage format, exact build targets, and deployment remain technical-validation decisions. This document prepares replacement boundaries without claiming these capabilities have already been proven.

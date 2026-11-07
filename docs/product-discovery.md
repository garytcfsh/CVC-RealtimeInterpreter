# Product Discovery: CVC-RealtimeInterpreter

Last updated: 2026-11-07. These are discovery notes; implementation authorization is recorded separately in the conversation. Latest confirmed choices supersede earlier alternatives. Continue discussion in English even when the user writes in Chinese.

## Product Goal and Users

Assist the church's human English interpreter during its second Sunday Mandarin worship service. Both Sunday services have substantially similar content. Currently, the interpreter hears the speaker through a dedicated headset and speaks through a dedicated microphone into attendees' headsets.

The final goal is Mandarin audio input and English audio output, with a bilingual screen allowing the interpreter to monitor, immediately interrupt AI speech, and interpret a portion personally. The interpreter operates the app themselves.

## 1. Confirmed Decisions

### Product Boundary, Platform, and AI

- Product: **CVC-RealtimeInterpreter**, supporting **Windows 11**.
- The app handles capture, display, playback controls, settings, and records. External AI accessed with a user-supplied API key provides transcription, translation, and speech generation.
- Initially support **one provider** for all three functions. Provider selection remains open.
- Cloud processing is acceptable, subject to selecting privacy terms the church accepts. Internet at church is reliable.
- Remember the API key securely on the computer; exclude it from configuration exports.
- Expected usage: 10 sessions × 90 minutes, approximately **900 minutes/month**. The previous **TWD 600 monthly budget is withdrawn**; no replacement limit is set.

### Stages and Session Control

- **Stage 1:** Display Mandarin transcription with corresponding English translation. The interpreter can read English or interpret from Mandarin.
- **Stage 2:** Retain the display and send synthesized English into the existing interpretation audio system alongside the human microphone; listeners keep the same headsets.
- Stage 1 remains available after Stage 2 arrives. Users explicitly choose the mode each session.
- Manual **Start/Stop** controls. Stage 2 audio begins automatically when finalized translations are ready.
- Cover announcements, prayers, Scripture readings, testimonies, and sermons. Input is Mandarin only.
- Prioritize the main speaker during overlapping speech rather than attempt to transcribe every voice.

### Audio Devices

- Select input and output in settings. The mixer is the primary source; Chrome/YouTube is an occasional backup/test source.
- Browser input captures only that browser's audio, not all system audio or the interpreter's microphone.
- Input switching is manual, without automatic fallback.
- Remember selected devices. Require replacement selection when a saved device is unavailable.
- Allow device changes during sessions without ending the record.
- Output disconnection mutes AI audio; require explicit **Resume AI** after selecting a replacement.
- No dedicated audio-test feature.
- Show a live input-level meter on Home and distinguish arriving audio packets, received silence, and no audio received. Device loss indicates unavailable input. This measures the app's captured samples without recording or uploading audio; it does not require a separate test mode.

### Live Display and Quality

- Interpreter's monitor only; no public projection.
- Two columns align **each Mandarin sentence with its English translation**, leaving vertical space between sentence pairs.
- Mandarin uses **Traditional Chinese**. Normal English uses **American English** spelling and wording; NIV quotations retain official wording.
- Continuously scrolling history, manual review, and **Return to live**.
- Current sentence: **background highlight**. Low-confidence Mandarin and English: different **text color**, visible together with the highlight.
- Sentence-sized segments. Show partial segments early with an **ellipsis only**, then replace them with finalized versions.
- Display target: within **8 seconds from the end of a short spoken phrase**, both Mandarin and English appear. Partial text counts. Long continuous sentences update progressively.
- Approximately **90% of normal content should be readable aloud without correction**. Detailed evaluation criteria are deferred; this goal is not yet validated.
- A single text-size control adjusts both columns.

### Stage 2 Playback and Intervention

- Speak finalized English segments only, as soon as ready. No review window, approval per sentence, or live editing.
- Single **Mute / Resume AI** on-screen control and configurable **global keyboard shortcut**. Warn when shortcut registration fails.
- Mute immediately, even mid-sentence. Transcription and translation continue silently.
- The interpreter chooses whether to mute or speak over AI; their microphone does not automatically mute playback.
- Resume discards interrupted and older queued audio and starts with the **newest available finalized sentence**.
- For automatic stale skipping, finish the sentence already playing, discard stale queued content, and resume recent content. Manual mute remains immediate.
- Default stale threshold: **15 seconds**, adjustable in settings and exported/imported.
- No strict 8-second speech target; establish acceptable speech delay through testing.
- Standard **American English** male/female voice chosen in settings; default **male**. No automatic voice matching or switching.
- Individual speaker voice imitation and adjustable speech speed are deferred.

### Scripture and Preparation

- Pre-service form: optional sermon topic and list of Bible references, e.g. `John 3:16–21`. Pasted Scripture text is not required.
- Match the **original Chinese Union Version (和合本)**, not its revised version.
- Automatically identify readings among pre-entered passages. Use official **NIV** on screen and in speech only for high-confidence matches.
- Automatically use normal translation for paraphrase, commentary, uncertain/incorrect matching, or passages not pre-entered. No whole-Bible identification or manual passage-selection fallback.
- No initial custom terminology/names list or first-service transcript-review/notes workflow.

### Interface and Configuration

- English and Traditional Chinese interface options. Default follows Windows: either Chinese variant selects Traditional Chinese; unsupported languages fall back to English.
- Themes: **System / Dark / Light**, default **System**.
- Save settings automatically.
- Active configuration lives under `C:\ProgramData\CVC-RealtimeInterpreter`.
- Export/import all user-adjustable settings, including theme, locale, text size, devices, voice, shortcut, and stale threshold. Exclude API keys and service records.
- Check imported device and shortcut availability on the destination computer; require reselection where necessary.

### Records and Exports — Both Stages

- Continuously save finalized Mandarin and English for crash/power-loss recovery. Do not save audio or temporary partial segments.
- Store locally under `C:\ProgramData\CVC-RealtimeInterpreter`, indefinitely until manually deleted.
- Date/time filenames; exact syntax remains unspecified.
- Stop automatically produces **Word, plain-text, and HTML** files. Word was originally the priority; all three are now required.
- Content consists only of transcript/translation pairs: no date/topic header or segment timestamps.
- Word/HTML use two sentence-aligned columns and a fixed printer-friendly light appearance. Preserve low-confidence text-color markings.
- Plain text: Mandarin followed by English, blank line between pairs, and `[Low confidence]` before affected segments.
- Preserve Mandarin-only segments with an **English translation unavailable** marker when translation failed.
- No in-app record browser or deletion; manage records through File Explorer.

### Stop, Recovery, and Save Failures

- Stop immediately ends capture and playback; allow up to **5 seconds** for the final partial segment to finalize.
- If finalization times out, omit the partial segment, save completed segments, and warn.
- Recover finalized data after a crash. Do not generate exports automatically; provide **Save recovered session** for manual export to all three formats.
- Each export saves independently. Save successful formats and offer **Retry** for failures.
- Continuous-save failure produces a prominent warning; live transcription, translation, and speech continue.
- Retain unsaved finalized text in memory and save it after the user presses **Retry**. Do not retry storage automatically.
- On Stop with unsaved text, retain it and offer **Retry saving**, **Save to another folder**, or **Discard unsaved text**. Confirm discard. Unsaved memory is vulnerable to process exit/crash.

### Connection and Provider Failures

- Internet loss: warn prominently, preserve history, mute AI, and automatically reconnect.
- Discard outage speech; no audio buffer/backlog recovery. Resume live transcription/translation automatically, but keep AI audio muted until explicit resume.
- Speech-generation failure: bilingual display continues; AI audio stays muted until user resumes.
- Translation failure: Mandarin continues and affected English is marked unavailable. Recovery resumes new live content without retrying missed translations.
- Missing/invalid API key blocks Start with corrective guidance.
- Exhausted provider credit: preserve history, mute AI, alert, and wait for explicit **Retry** after resolution instead of repeated retries.

## 2. Proposed Recommendations — Not Yet Decisions

- Obtain a clean, separate mixer feed without disturbing the interpreter's headset path.
- Validate one provider on representative church speech for quality, latency, confidence signals, privacy, and operating cost before committing to it.
- Confirm a permitted source and usage terms for official NIV text.
- Evaluate the 90% usability goal with recordings and bilingual interpreter review; detailed scoring rules were explicitly deferred.

## 3. Open Questions and Technical Validation

- **Hardware:** AUX availability, connectors, signal level, clean main-speaker mix, audio interface/adapter, and physical output routing alongside the human microphone.
- **Provider:** Select the single supported provider and validate all three functions, privacy terms, credential storage, account setup, rate limits, and costs.
- **Scripture:** Text access/usage rights; accurate original-CUV-to-NIV matching and transitions into commentary.
- **Timing/confidence:** Phrase boundaries, segment finalization, low-confidence criteria and provider support, precise stale-age measurement, and queue behavior when no sufficiently recent sentence exists.
- **Capture:** Windows 11 browser-only capture and selection when several Chrome windows/tabs play audio; realistic main-speaker prioritization from the church feed.
- **Persistence:** Internal recovery format, filename syntax/collisions, ProgramData permissions, and closing with unsaved text.
- **Quality:** Detailed scoring and realistic speech-delay targets; verify the 8-second display target and 90% usability goal through testing.
- **Scope:** Songs/music are not included in confirmed transcription scope. Installation/update approach and detailed visual design remain unspecified.

### Working Assumptions

- A mixer AUX output is probably available; this has not been verified.
- Pre-entered Bible references can be resolved to appropriate texts, subject to access, rights, and implementation validation.
- Provider and capture capabilities can support the intended workflow; no feasibility prototype has established this yet.

## Trade-offs and Superseded Choices

- More speech context improves translation but adds delay. Partial text supports fast display; audio waits for finalization.
- Live editing, a 2-second review window, and a separate Interpret myself action were discussed and **removed**. Human intervention now uses one Mute / Resume AI control.
- Automatic stale skipping finishes the playing sentence; manual mute interrupts immediately.
- Newest-sentence resume and live-only outage recovery intentionally omit older content to avoid accumulating delay.
- Manual male/female voice selection replaces automatic speaker classification/switching. Individual imitation is a later-stage feature.
- Sentence-pair alignment replaces the briefly discussed paragraph layout.
- The API-based product boundary does not remove cloud privacy, cost, or reliability dependencies. The TWD 600 budget was withdrawn.
- Continuous saving protects records; continuing interpretation through disk failures prioritizes the live service while leaving unsaved memory at risk.
- High-confidence NIV substitution reduces wrong-passage output but may miss correct readings; normal translation is the fallback.

# Product Discovery Notes

## Purpose and Context

This Windows application will assist a human English interpreter during the church's second Chinese worship service. The church holds two substantially similar Chinese services on Sundays. A human interpreter currently listens to the speaker through a dedicated headset and delivers English through a dedicated microphone to foreign attendees' headsets.

The product is an assistant, not a replacement for the interpreter.

## Confirmed Decisions

### Delivery Stages

- **Stage 1:** Display a live Mandarin transcript alongside its English translation. The interpreter may read the English output or interpret directly from the Mandarin transcript.
- **Stage 2:** Retain both live transcripts and synthesize the English translation as audio for the interpretation system. The human interpreter monitors the output and intervenes when needed.

### Service and Language Scope

- Input language is Mandarin only; no other Chinese varieties are required.
- The live text scope includes announcements, prayers, Scripture readings, testimonies, and the sermon.
- The Stage 1 display is for the interpreter's dedicated monitor only, not a public or projected congregation display.
- Songs have not been included in the confirmed scope.

### Interpreter Display

- Show continuously scrolling transcript history.
- Let the interpreter manually scroll back through earlier content.
- Provide a control that returns the view to the current/live line.
- Visually distinguish the current line, including with a different color.
- Target a maximum of **8 seconds** from spoken content to display of Mandarin transcript and English translation.
- Mark low-confidence text for interpreter attention; do not require the interpreter to edit output before it appears.

### Scripture Preparation and Review

- The interpreter can enter the sermon topic and Bible passages before the sermon; speakers usually provide these in advance.
- When displaying recognized Scripture, the required English Bible translation is **NIV**.
- A first-service post-review, correction, and note-taking workflow is not needed in the initial scope.

### Environment

- The church has a mixer.
- Reliable internet access is available during the service.

## Working Assumptions

- An AUX output from the mixer is probably available for the application. This has not been verified with the sound technician or hardware inspection.
- The two Sunday services are similar enough that the second-service context described above is the intended operating setting.

## Proposed Recommendations

These are not product decisions yet.

- Obtain a separate, clean mixer feed for the Windows computer so the app does not disturb the interpreter's headset signal.
- Use the 8-second budget to balance fast updates against sufficient speech context for a better translation.
- Consider cloud speech/translation services for the initial version because internet is reliable, after evaluating privacy, reliability, and recurring cost.
- Confirm NIV copyright and permitted-use requirements before displaying NIV text in the product.

## Open Questions

- Verify the AUX output: connector type, line level, whether it is unused, and whether it contains a clean speaker-only mix.
- Define how the Windows computer will physically receive that mixer output (for example, required audio interface or adapter).
- Define Stage 2 routing precisely: should synthesized English enter the existing interpretation audio chain alongside the human microphone, and how should the interpreter take over immediately?
- Decide whether the system should use the pre-entered passage to show official NIV wording, translate spoken Mandarin normally, or use another fallback when the speaker departs from the listed passage.
- Define how low-confidence text is identified and presented without distracting the interpreter.
- Confirm whether an interpreter needs a way to correct, suppress, or otherwise control a problematic live segment.
- Choose the synthesized English voice and establish acceptable behavior for mispronunciations, pauses, audio failures, or internet outages in Stage 2.
- Confirm handling for music or sung content; it is currently outside the confirmed scope.

## Key Trade-offs Recorded

- Longer speech segments improve translation context, but increase delay. The agreed limit is 8 seconds.
- A scrolling history improves recovery and context, while auto-follow plus a return-to-live control preserves readability during live interpretation.
- Cloud services may accelerate an initial solution, but introduce ongoing cost and connection/privacy dependencies.
- Automated Stage 2 speech reduces interpreter workload, but needs a safe, fast human override path to avoid broadcasting errors.

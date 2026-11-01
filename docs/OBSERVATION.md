# Local observation pipeline

Implemented in v0.6.0. Screenshots stay on the PC. Observation does not issue game inputs.

`ObservationService` decodes an ADB PNG, samples a grayscale fingerprint, runs Windows OCR when due, classifies the resulting text, extracts conservative resource readings, and commits a structured `ScreenObservation`. OCR runs on a worker task behind a single-job gate. Live preview retains its separate 1/2/5/10-second setting.

## Cadence and limits

- A 32 × 32 grayscale sample detects changes. Mean pixel difference of at least 2.5, or at least 5% of samples changing by 12 levels, triggers a meaningful change.
- Changed screens receive OCR at most every 10 seconds. A 60-second refresh catches subtle counters that the fingerprint may miss. Device or resolution changes trigger immediate recognition. Manual analysis bypasses the cadence.
- Frames arriving during an OCR job are skipped. OCR has a 20-second cancellation deadline; failures surface in the UI without stopping preview.
- Input is limited to 32 MB and dimensions of 1–8192 pixels per side. OCR scales proportionally to at most 1800 pixels on the longest side, also respecting the engine limit.
- Results contain at most 2000 word boxes and 60,000 text characters. Coordinates are mapped back to the original Android framebuffer. Desktop window coordinates are never substituted.

English OCR is preferred when installed; otherwise the engine tries available user-profile languages. Windows can return no OCR engine if no suitable language is available. Install the language's OCR support through Windows Settings when necessary. [Microsoft API documentation](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.trycreatefromuserprofilelanguages?view=winrt-26100).

## Meaning and confidence

The structured record includes timestamp, device, dimensions, orientation, sampled fingerprint, OCR language, text, word rectangles, classification evidence, rule version, and labeled resource readings.

Screen recognition uses English phrase rules with whole-word matching. Unsupported text and close competing classifications remain `Unknown`. The displayed percentage is a heuristic rule score. It is not calibrated accuracy or OCR confidence; the Windows OCR API used here exposes no word-confidence value. A classification cannot authorize gameplay input.

Resource extraction currently recognizes explicitly labeled B-Tads and W-Tads. It supports ordinary numeric amounts and K/M/B suffixes, rejects malformed grouping, overflow and progress fractions, and discards conflicting values for the same resource. These are unverified readings of visible labels, which might describe a cost rather than an account balance. They do not establish spendable balances or authorize spending. Icon-only resources are not inferred.

## Persistence and replay

The app stores observations in `%LOCALAPPDATA%\SnailCommander\Observations`. Each record is written to a temporary file, flushed to disk, and atomically renamed. A lock serializes independent writers. Existing records remain until the next record commits. Incomplete temporary files and invalid JSON records are ignored when loading.

The default retention is 3000 records, each at most 512 KB; images are managed separately by the capture library. Failed cleanup can temporarily leave one extra record, but the next write must clear that excess before adding more. Stale temporary files receive best-effort cleanup. This is bounded file-backed memory, not a transactional account database or neural-model training.

The Intelligence page loads the latest 100 records, exposes recognized text and evidence, analyzes current/image-file captures, exports JSON, and replays saved observations. Replay reruns classification and resource extraction against stored OCR text in timestamp order. It does not rerun image OCR or train a model.

## Validation on 2026-10-31

Real local Windows OCR integration produced:

| Observed screen | Framebuffer | Word boxes | Elapsed |
| --- | --- | ---: | ---: |
| BlueStacks launcher | 960 × 540 | 26 | 240 ms |
| Super Snail event notice | 540 × 960 | 50 | 203 ms |

These single-run timings include OCR, persistence, replay and unchanged-frame suppression checks after capture. They verify real landscape/portrait processing and framebuffer bounds. They are not an accuracy benchmark or a sustained reliability test. The event notice does not establish event rules or scheduling facts.

Deterministic tests cover classification, ambiguity, parsing, cadence, orientation/device changes, persistence, corruption handling, retention and replay. Run the explicit Windows integration harness with `--image <path>` or `--adb <adb-path> --device <serial>` to exercise real OCR. Labeled screen fixtures, text-error measurements, multilingual coverage, icon recognition and broad in-game classification remain next steps.

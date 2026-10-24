# Snail Commander architecture

## Goal

A local-first observation and strategy companion for Super Snail in BlueStacks. Aim for a low-maintenance workflow through reliable account analysis, persistence, and progress reporting. **Autonomous gameplay is neither implemented nor validated**, and the game's published terms may restrict automated gameplay.

## v0.1.0

- Windows Forms dark dashboard
- Detect BlueStacks' HD-Adb.exe or Android SDK adb.exe
- Connect to a user-supplied local ADB endpoint
- Enumerate connected ADB devices
- Read-only binary screenshot capture using exec-out screencap -p
- Save local PNG captures and diagnostic logs

## Safety and reliability boundaries

- No taps, swipes, gameplay interactions, currency spending, upgrades, or in-game purchases
- No credentials or secret tokens stored
- No screenshot uploads
- Capture failures are visible and logged
- Display dimensions are **detected from the actual screenshot**, not assumed from the size of the BlueStacks desktop window

## Follow-on modules

- Screen type classification and OCR
- Confidence-scored structured account snapshots
- Local reference database / progression strategies
- Local compact vision-language model (RTX 4070 Ti 12 GB)
- Saved comparisons and weekly summaries
- Error categorization, recovery for observational workflows

## Environment

- Windows 11 Pro
- Intel i9-14900K, RTX 4070 Ti 12 GB, 128 GB DDR5
- BlueStacks 5
- .NET 8 WinForms, self-contained win-x64 build

## Validation

GitHub Actions compiles the application. Real ADB connectivity and screenshot quality must be tested locally on the user's Windows PC.

No promises of 7-day autonomy before end-to-end tests verify it.

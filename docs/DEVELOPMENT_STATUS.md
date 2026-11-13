# Development status

## Current session — 0.8.1 (2026-11-13)

Baseline public/local main: 1287b6a2e11b1352d6e62b13b9efaaf7a41e69c0, following verified 0.8.0. The clean development checkout was preserved at Games/SnailCommander-Development during the requested release-folder cleanup. Normal application data is unchanged.

## Implemented

- Added Game navigation and a header Game panel button. The full-height panel contains the game image and manual control, Android Back and minimize/show BlueStacks controls.
- Local screenshot mirroring targets a 500 ms cadence while Game is visible, sharing the existing nonqueuing capture gate and independent OCR cadence. Portrait/landscape frames remain aspect-correct. Images are disposed on replacement.
- User-directed taps and release-time swipes map through pure Zoom/letterbox geometry. Black borders, stale frames, changed device/orientation and a non-Super-Snail foreground package reject input. A second input waits for a new post-input frame; there are no queued gestures or blind retries.
- Enabling manual control stops/drains automation. Starting automation or installing an update cancels/drains manual input. Switching away disables it. Closing cancels input and restores a BlueStacks window minimized by this app.
- Minimize/show works only with one identifiable player window; multiple windows require manual selection/minimization. The emulator keeps running. Process identity is checked before restoration.
- Preserved existing observation/account evidence, advisory optimizer, bounded automation, updater and profile behavior.

## Verification

- 230 deterministic assertions passed, including 18 new viewport checks for portrait/landscape borders, resize scaling, exact bounds, invalid dimensions and nonfinite coordinates.
- App builds passed with no warnings/errors. App/updater version is 0.8.1.
- Live GUI test used only the disposable game-panel-ui profile. The in-app game image continued updating while BlueStacks was minimized. Leaving/reopening Game disabled manual control, and closing restored the minimized BlueStacks window.
- One tap in the mirrored game image opened the real game's Settings screen. A drag was forwarded; a tap on its return arrow returned to the home screen. No game settings, purchases, upgrades or resource actions were requested during this test. Live interaction is a small manual smoke test, not an unattended gameplay benchmark.
- Local real updater-process tests passed all 84 assertions and profile checks passed all 12. Final public release verification is recorded below after completion.

## Limits

This is interactive screenshot mirroring and ADB input forwarding, not native cross-process window embedding or video streaming. Target cadence is about two frames per second and depends on ADB/emulator latency. No keyboard/text, audio, multi-touch, long-press or continuous drag feedback is implemented. It requires BlueStacks with ADB enabled and Super Snail foreground for input. A missing connection is still handled from Home/Settings.

The 0.8.0 bounded automation remains narrow: login Start, free reward OK and explicit update notice dismissal only. Most gameplay, login-error recovery, inventory ingestion, strategic scheduling and seven-day unattended operation remain unfinished. Manual control is separate from these policy rules and follows the user's gestures. OCR/evidence/optimizer limits still apply; persistence is not neural training.

## Release delivery

Pending final release verification. Existing 0.8.0 delivery remains available until 0.8.1 is verified. Keep the release folder tidy with the current app folder, current ZIP and immediately previous ZIP for rollback; development files stay outside it.

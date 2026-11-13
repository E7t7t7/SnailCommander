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
- Local real updater-process tests passed all 84 assertions and profile checks passed all 12. Final CI passed the same gates plus 230 deterministic checks (326 assertions total).

## Limits

This is interactive screenshot mirroring and ADB input forwarding, not native cross-process window embedding or video streaming. Target cadence is about two frames per second and depends on ADB/emulator latency. No keyboard/text, audio, multi-touch, long-press or continuous drag feedback is implemented. It requires BlueStacks with ADB enabled and Super Snail foreground for input. A missing connection is still handled from Home/Settings.

The 0.8.0 bounded automation remains narrow: login Start, free reward OK and explicit update notice dismissal only. Most gameplay, login-error recovery, inventory ingestion, strategic scheduling and seven-day unattended operation remain unfinished. Manual control is separate from these policy rules and follows the user's gestures. OCR/evidence/optimizer limits still apply; persistence is not neural training.

## Release delivery

- Published code commit: 8ffeb233884856d559a2168f9b2aa982db3c9a53.
- Public release: [v0.8.1](https://github.com/E7t7t7/SnailCommander/releases/tag/build-8ffeb233884856d559a2168f9b2aa982db3c9a53).
- [CI 43518009220](https://github.com/E7t7t7/SnailCommander/actions/runs/43518009220): build and publish-release both succeeded, with 326 assertions passing before packaging.
- Downloaded the actual public ZIP, 102011481 bytes; SHA-256 matches GitHub: b6c405eccb0cc81954f44f413c800e778a6b96ac2ddcde3c99fa0e5a1c2f3654.
- Local delivery: Games/SnailCommander/SnailCommander-v0.8.1/SnailCommander.exe, with SnailCommander-v0.8.1.zip beside it. Verified ProductVersion exactly 0.8.1+8ffeb233884856d559a2168f9b2aa982db3c9a53.
- The normal 0.8.0 app was still open at delivery, so its installation and existing rollback ZIPs were preserved. Close the old copy before opening the new one to avoid duplicate frontends. Development files stay outside the release folder; the old empty source placeholder remains locked by a process.
- Use Game (or Game panel in the header), Enable manual control for taps/drags, and Minimize / show BlueStacks to use only the commander window. Automation may be watched in Game without enabling manual control.

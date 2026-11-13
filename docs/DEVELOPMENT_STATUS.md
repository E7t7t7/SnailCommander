# Development status

## Current version

- App/updater: **0.7.0**, session date 2026-11-13 (America/New_York).
- Baseline: `16e95eca2789bfda14e8d3b67c90f6b65f99684a`; local and public main matched, with no user changes.
- Last verified public release at session start: [v0.6.2](https://github.com/E7t7t7/SnailCommander/releases/tag/build-f9511c92f84ceb4a81fa4f5f14d77f61f4fc4c85). v0.7.0 release publication is pending final CI verification.

## Completed this session

- Added a pure labeled-field extractor and partial account evidence projection in Core/Accounts. Leadership and individual Minion MAX remain independent; missing values remain unknown.
- Added explicit observation provenance: LiveAdb, ImportedImage, Replay, Synthetic and legacy Unspecified. Imported/replayed/synthetic/legacy records cannot populate the live evidence projection.
- Added unique minion-name binding from explicit labels or exact known nonsynthetic inventory names. Unnamed cards never corroborate across frames. Multiple names stay unassigned.
- Added Single/Repeated/Rounded/Unassigned/Conflicting/Stale state, a ten-minute freshness policy, duplicate/time/device separation, simultaneous-conflict handling and source evidence. Repeated readings are not calibrated OCR confidence.
- Added the Account GUI page with field/state/time/context rows, source text, refresh and JSON export. It does not overwrite optimizer inventory. GUI smoke verified the page and controls at the normal window size in an isolated profile, including the correct empty state.
- Added adaptive OCR: native first; bounded enlargement only for Unknown screens and selected only when it resolves screen classification. Scale, pass count and fallback notes persist. One image with two OCR passes is still one observation.
- Kept the public updater, isolated profiles, rollback, retained observation history and existing optimizer working.

## Verification

- Baseline: 124 deterministic assertions passed.
- Current deterministic suite: **178 assertions, 0 failures**, including 54 additional account/provenance/metadata checks.
- Windows profile-path checks: **12 assertions passed**.
- Real updater process suite against the expanded GUI: **84 assertions passed**, across all eight existing scenarios, including failed startup, grace-period exit, copy/backup failures, killed-worker recovery and actual GUI installation/startup.
- Local real Windows OCR -> extraction integration: **8 checks passed**, producing 11 labeled fields from rendered synthetic text. Selected numbers, name binding, framebuffer bounds, persistence and exclusion from live account state were verified. This is a synthetic fixture, not a real game account.
- Local builds and self-contained publishes passed. App/updater builds have no compilation warnings/errors.
- Real emulator observations were collected locally: 960x540 launcher and 540x960 game notice, navigation, profile and reward screens. Captures/OCR containing player/chat details stay in ignored local artifacts and are not published.
- OCR comparison: the native notice was Unknown; adaptive enlargement resolved it to Events in about 374 ms including persistence/replay. Native synthetic-card OCR retained HP/ATK; forced enlargement dropped HP and misread ATK. This measured regression is why recognized screens keep native OCR. Other sampled game screens remained Unknown. No representative accuracy percentage is claimed.
- Normal user application data was not used for GUI/update tests. The UI test used `account-ui-smoke` with preview disabled and closed cleanly.

## Manual game-inspection outcomes

Manual ADB navigation opened the already-selected account and inspected menus. A facility interaction produced a Collect Bodies reward, and a later navigation interaction produced a Memory Module/Rewards Gained popup. These were real game changes, not read-only capture. No cloning order, upgrade or paid/premium expenditure was intentionally issued; before/after currency balances were not independently audited. The navigation was stopped rather than promoted into an unattended routine. The shipped application still contains no gameplay input controller. Reward gains are explicitly rejected as inventory totals by the new extractor.

## Current working product

- Local .NET 8 WinForms command center, live ADB preview, capture library, persistent preferences/logs and isolated profiles.
- Local Windows OCR, conservative screen rules, structured text regions/resources, independent observation cadence, retained atomic history and replay.
- Partial labeled account/minion evidence with provenance/freshness/conflict checks and a GUI inspection/export path.
- Evidence-tagged user-entered minion inventory, bounded composition optimization and reserve-aware advisory cloning plans.
- Public token-free release checks/downloads, SHA-256/package validation, external replacement worker, startup acknowledgment, rollback and process-tested next-attempt interrupted-update recovery.

## Limits and unfinished work

- **Not an autonomous player.** No verified account inventory ingestion, gameplay controller, persistent gameplay scheduler, learned navigation, battle predictor, strategic local model, full equipment/form/relic/partner optimizer, event planner, weekly report or seven-day operation exists yet.
- Positive minion-detail extraction has been tested through typed/rendered synthetic fixtures. A representative positive real minion-card corpus and actual own-account identity validation are still missing. Do not claim the real inventory is populated automatically.
- Strict labels do not resolve icon-only resources or every game layout. Displayed stats may be base or effective; their gameplay meaning is not inferred. Modes without explicit supported text remain unspecified. Missing labels and ambiguous numbers remain unknown.
- Device identity does not prove account identity. Switching game accounts on one emulator is not automatically detected. This is one reason evidence does not patch optimizer data automatically.
- Evidence lookback is bounded to 200 recent records in the GUI, with 3,000 retained observation files. Stale/evicted fields cannot be assumed current. Legacy records without provenance are retained for inspection but excluded from live state.
- Repeated OCR is not neural training or a win/casualty model. Existing troop scores remain explicit stat heuristics with bounded-search limitations.
- User-facing manual rollback selection, global capture/log/temp cleanup, automatic relaunch of a killed updater and power-loss guarantees remain incomplete. Interrupted updates recover on a later attempt, not through an independent watchdog.
- The known narrow GitHub Latest promotion race remains; missing integrity metadata safely prevents installation.

## Next priority

1. Collect and label real own-account minion detail/deployment screens; validate names, per-field layouts, quantities and base/effective stat semantics. Add account/session identity before allowing evidence to update inventory.
2. Expand deterministic recognition/region parsing and establish a held-out real-game accuracy benchmark. Keep private screenshots/OCR local.
3. Add a verified inventory-ingestion/review transaction with freshness and coherent quantity constraints; then connect it to troop optimization.
4. Add bounded ADB actions with expected transitions, cancellation, source-checked targets, action journals and postcondition tests. The manual navigation side effects show why this must precede unattended gameplay.
5. Add persistent task scheduling/resource policies, recovery and soak tests; complete rollback selection and bounded cleanup.
6. Benchmark compact local VLMs only when they improve measured recognition. No model download or paid inference service was introduced.

## Durable architecture

Preserve existing Windows application/updater behavior. Pure observation/account/strategy/update logic stays in Core; Windows OCR and GUI remain adapters. Account evidence is derived from persisted observations with source metadata, not model training or an authoritative database balance. See ACCOUNT_EVIDENCE.md for exact labels, scopes, states, OCR selection and reproduction commands; other subsystem docs remain applicable.

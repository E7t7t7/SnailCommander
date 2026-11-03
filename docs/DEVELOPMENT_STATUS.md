# Development status

## Current version and release

- Application/updater: **0.6.2**, work completed 2026-11-02 (America/New_York).
- Source checkout: `source/` inside the SnailCommander workspace. Session baseline `c185e8261a0d0eaeb5939e2bfede1d921c1d31ee` matched public main with no user changes.
- Last verified public release at session start: [v0.6.1](https://github.com/E7t7t7/SnailCommander/releases/tag/build-3f3bebe212a59d17d5f4fbed31b9c40cff245b24). v0.6.2 publication is pending final CI verification; build tags remain `build-<40-character commit>` for existing updater compatibility.
- Existing installed folders, normal preferences, account data and capture history are preserved. This session's GUI/update tests used disposable custom profiles only.

## Completed this session

1. Added reproducible real Windows updater process tests, using the published worker executable, child-process fixtures, and the actual published GUI.
2. Validated successful replacement; immediate startup exit; no acknowledgment before deadline; exit during the grace period; mid-copy failure; backup creation failure before a transaction exists; recovery on a later attempt after forcibly terminating an updater; and successful installation/startup of the actual GUI.
3. Fixed a recovery defect: when backup creation fails after the old app exits but before a journal exists, the worker now reopens the unchanged previous installation.
4. Added startup-only `--data-root` profiles. Settings, captures, observations, minion JSON, logs and update backups are isolated; the profile propagates through update handoff, replacement startup and rollback restart. Default data locations remain unchanged. Custom profiles cannot be drive roots or reside inside the application installation.
5. Added bounded, validated diagnostic worker options and independent locks for isolated diagnostic data roots. Normal clients retain their original mutex and 45-second/three-second health timing. Attempt-owned health files are removed on success and failure.
6. Made process tests and default/custom profile-path checks mandatory CI release gates. A JSON process report is retained as a CI artifact. Test-only process probes are not included in the product package.
7. Derived GUI version labels from assembly metadata; custom profiles display their directory name in the title.

## Verified product capabilities

- Existing dark WinForms shell, automatic ADB discovery/device selection/reconnection, live preview, capture library, local preferences and public GitHub updating.
- Local Windows OCR, conservative English screen classification, structured evidence/text regions/resources, actual framebuffer resolution/orientation, independent changed-screen cadence and bounded atomic observation history/replay.
- Evidence-tagged minion input models, independent Leadership and per-type MAX limits, ready/deployed/healing quantities, supplied threshold/mode/type bonuses, bounded composition search and explainable stat-based results. Advisory clone planning respects explicit resource reserves.
- Minion account import/edit/save and clearly labeled synthetic examples; Intelligence inspection/export/replay; public release notes, periodic update checks, optional verified download and explicit installation.
- External installer, SHA-256 verification, ZIP/path/size limits, persistent replacement journals, startup acknowledgment, rollback, configurable known-good backup retention and next-attempt interrupted-update recovery.
- Saved window geometry and bounded GUI log text; custom profiles preserve normal storage behavior unless explicitly selected.

## Verification results

- Baseline at session start: 113 deterministic assertions passed.
- Current deterministic regression suite: **124 assertions, 0 failures**.
- Real Windows process suite: **84 assertions, 0 failures**, across eight scenarios. Verified actual exit codes, on-disk journal states, successful old-app restart, cleanup of new package files after rollback, preservation of unrelated files/account inputs, replacement responsiveness, and profile-local GUI logs/captures.
- Profile paths: **12 assertions, 0 failures**. Default paths, malformed/duplicate/root/installation-contained arguments, custom preferences persistence, and isolated capture history are checked without loading or modifying normal user data.
- Release app, updater, process fixture and test projects compile/publish successfully. Process tests use self-contained Windows executables; OCR is not mocked into the GUI health handshake.
- The killed-worker scenario terminated only its verified test worker while its replacement remained alive. The following update attempt recovered the pending journal, restored old files, and completed a fresh healthy installation. This is next-attempt recovery, not automatic worker relaunch.
- Tests stop only processes whose executable paths match their disposable installations. Passing test folders are removed after absolute-parent/owner checks; failure evidence is retained under ignored artifacts.
- No live gameplay action, purchase, premium expenditure, seven-day soak, or new OCR-accuracy benchmark was performed this session. The real GUI case disables preview and uses an empty fixture account.

## Earlier integration evidence

The preceding session verified actual BlueStacks ADB/OCR on `127.0.0.1:5555`: launcher **960x540**, 26 OCR regions, `EmulatorHome`, approximately 240 ms; Super Snail event notice **540x960**, 50 OCR regions, `Events`, approximately 200-203 ms including persistence/replay. These are two measured integration samples, not an accuracy corpus. A later visual GUI recheck was blocked by Windows capture helper `CreateForMonitor (0x80070057)`; no DPI/scaling matrix is claimed. v0.6.1 also corrected duplicate SDK revision metadata causing same-build update offers.

## Incomplete and known limitations

- This remains **observation and advisory optimization**, not an autonomous player. No authoritative account/minion extraction, verified game catalog, model inference, gameplay navigation/action controller, persistent gameplay scheduler, combat outcome prediction, equipment/relic/form/partner optimizer, event strategy, weekly report, or unattended agent loop exists yet.
- Troop scores are explicit stat heuristics, not damage/casualty formulas, win probabilities or guaranteed globally optimal formations. Mode capacity must be supplied; only supplied bonuses are applied. Cloning is an advisory core API and never executes in-game spending.
- OCR rules are incomplete and English-oriented. Unreadable/conflicting fields remain unknown. Observation records are not an authoritative account balance.
- Manual rollback selection is still incomplete; Open update history opens journal/backups only. Failed/legacy backups and temporary ZIP/staging folders can accumulate. Legacy screenshot archives/disk logs are not fully retention-managed.
- A terminated worker does not relaunch itself; later-attempt recovery is now process-tested. Power-loss durability, every historical binary upgrade, interactive UI click-through installation, and seven-day reliability are not established by these tests.
- Public GitHub Latest checking and promotion are separate API calls; the known narrow race with an external publisher remains. Missing integrity metadata safely blocks installation.

## Next priority

1. Build a labeled real-game screen corpus and measure OCR/classifier precision. Implement provenance/freshness-aware account and minion extraction instead of requiring account JSON.
2. Research and record verified game mechanics and minion bonuses with source/date/applicability/confidence; do not invent names, reset schedules, damage formulas or thresholds.
3. Introduce bounded ADB actions, expected transitions, cancellation, action journals and postcondition tests; complete one verified free-reward/navigation loop before broad gameplay scheduling.
4. Add persistent task scheduling/checkpoints/resource policy, controlled recovery and extended soak tests before unattended claims.
5. Complete user-facing rollback selection and bounded temporary/capture/log cleanup.
6. Benchmark compact local VLMs only when held-out recognition measurements justify them; no model or paid inference service is installed.

## Architectural decisions

Preserve .NET 8 WinForms and token-free public updates. Pure observation, strategy and update policies remain in Core; Windows adapters and GUI stay separate. Use bounded flushed atomic JSON observation records until account-wide transactions justify a database. Isolate all test state through explicit local profiles. Persistent history is account memory, not neural training. See ARCHITECTURE.md, OBSERVATION.md, MINION_OPTIMIZER.md, LOCAL_INFERENCE.md and UPDATER.md.

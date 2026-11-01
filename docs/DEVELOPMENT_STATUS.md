# Development status

## Session and version

- Application: **0.6.1**. Source baseline audited: `main` at `f1c62c8854183a8b11a4e69627c4ec58966d05fc` (v0.5.1).
- Source checkout: `source/` inside the user's SnailCommander workspace; the original workspace contained extracted releases, not a Git checkout. Existing installed copies were preserved.
- Last verified public release before publishing this session: `build-f1c62c8854183a8b11a4e69627c4ec58966d05fc`.
- Release 0.6.0 (`6979ef846c863cafb6cdcbc9326537879bf2fb9b`) passed CI and published. Download verification caught duplicated SDK revision metadata causing same-build update offers; corrected in 0.6.1, pending final CI verification. Public release publishing runs automatically on a successful push to `main`; build tags remain `build-<commit>` for existing updater compatibility.
- Session dates: 2026-10-31 to 2026-11-01, America/New_York.

## Verified working

- Original v0.5.1 app builds before changes; existing ADB, preview, capture library, connection preferences and public update mechanisms retained.
- Platform-independent Core project separates observation/classification, minion strategy and updater policy from Windows adapters/UI.
- Windows built-in OCR runs locally on worker tasks; English phrase rules classify supported screens with evidence and an explicit heuristic score. Unknown/ambiguous screens stay Unknown.
- Framebuffer dimensions/orientation and OCR regions map to the Android screenshot rather than the emulator desktop window.
- Preview and observation cadences are independent: changed-screen OCR at >=10 seconds, periodic refresh at 60 seconds, one observation task at a time, 20-second OCR deadline.
- Labeled B-Tad/W-Tad text can produce unverified readings. Icon-only values, malformed numbers, progress ratios and conflicting readings do not establish balances.
- Atomic, retained observation history survives restarts; Intelligence displays text/evidence/coordinates, imports an image, exports JSON, and replays recorded OCR text through current rules.
- Evidence-tagged minion account JSON, validation, ready/deployed/healing quantities, individual MAX limits, Leadership, mode/type/threshold bonuses and bounded stat-based composition search.
- Minion Command imports/edits/saves input accounts, selects goals/modes, and explains ranked compositions. Invented example values remain conspicuously synthetic; they never automatically populate the real account.
- Reserve-aware cloning queue API is implemented/tested; no GUI cloning execution exists.
- Updates page, startup/six-hour public release checks, release notes, optional verified background download, explicit install action, configurable 1-10 backup retention.
- Hardened SHA-256/ZIP limits, file replacement journal, startup acknowledgment, failure rollback, and interrupted-update recovery on the next updater attempt.
- CI runs deterministic regression before self-contained packaging, includes release notes and checksums, derives the version from csproj and checks current main before Latest promotion.
- Saved window geometry; bounded GUI log text; navigation scroll access. Overview controls scroll when vertical space is constrained.

## Verification results

- Baseline Release build: 0 warnings, 0 errors.
- Updated app Release build: 0 warnings, 0 errors.
- Updater Release build and self-contained app/updater publish: successful.
- Deterministic regression runner: **113 assertions, 0 failures**. Covers device parsing, classification/resource parsing, cadence/resolution/device changes, retained persistence/replay, minion constraints/synergies/objectives, bounded search, brute-force comparison, cloning reserves, provenance, digest/path checks, install/rollback/journal/retention, startup nonce acknowledgment.
- Actual BlueStacks integration: started installed Pie64 instance; both `emulator-5554` and `127.0.0.1:5555` connected. Capture from 127.0.0.1:5555 worked.
- Live landscape launcher: **960 x 540**, 26 OCR regions, `EmulatorHome`, about **240 ms** including persistence/replay checks.
- Live Super Snail event notice: **540 x 960**, 50 OCR regions, `Events`, about **200-203 ms** including persistence/replay checks. Visible stylized text included OCR mistakes; no text-accuracy or classification-accuracy benchmark is claimed.
- GUI smoke: live preview and portrait resolution changes; synthetic input visibly labeled; five ranked composition results calculated in approximately 19 ms on the initial GUI fixture. UI review exposed sidebar horizontal overflow and text clipping, corrected before final packaging.
- Final packaged app launched successfully. A later visual recheck was blocked by the Windows capture helper returning `CreateForMonitor (0x80070057)`; the earlier GUI smoke and subsequent clean builds are the available evidence, not a verified DPI/scaling matrix.
- The small deterministic optimizer benchmark compared beam search (~11-14 ms, 23 evaluations) with exhaustive enumeration (~1-2 ms, 15 legal compositions); validation/setup overhead dominates this tiny case. No claim that beam is always faster.
- Sandbox initially denied atomic temp-file renames; required regression runs passed with normal Windows filesystem access. A build attempted while GUI test processes held a Core DLL was retried after closing those test instances, then passed.
- No real installed-app update/failed-startup rollback end-to-end test yet; file transaction and acknowledgment logic have deterministic tests. No seven-day soak test, gameplay action, purchase, or account spending was performed.

## Incomplete and known limitations

- This release is **observation plus advisory optimization**, not an autonomous player.
- There is no authoritative account extraction, verified per-minion game catalog, full game-mechanics knowledge database, neural-model inference/training, navigation/action controller, persistent gameplay scheduler, battle predictor, equipment/relic/form/partner optimizer, event planner, weekly report or unattended recovery loop.
- All troop scoring weights are explicit heuristics. No damage formula, win probability, casualty estimate or globally optimal formation is claimed. Only supplied mode bonuses are applied; mode capacity must be supplied through the account Leadership constraint.
- Cloning queue/resource reserves are a core API; the GUI does not execute cloning or resource decisions.
- OCR languages depend on Windows support; English rules are incomplete and game updates require fixture validation. The game notice reset text was not trusted as a verified server schedule.
- Manual rollback selection dropdown is not implemented. Backup journals/files exist; automatic rollback applies to failed updates. Retention prunes healthy journaled backups only; legacy/failed backups and temporary staging can still require cleanup.
- A killed updater is not automatically relaunched; a later update attempt recovers unfinished journals. No seven-day reliability claim.
- Screenshot archives and disk logs still use the existing retention behavior; observation JSON and GUI buffers are bounded, but total legacy capture/log storage is not yet fully managed.
- The Latest check reduces obsolete-release promotion, but GitHub ref reading and Latest mutation are not atomic. Network/digest metadata availability can block updates safely.

## Next priority

1. Complete a real self-update/failure rollback smoke test on a disposable installation and add repeatable Windows process integration coverage.
2. Build a labeled corpus of real game screens and measure OCR/classifier precision and coverage. Establish verified account/minion extraction with field provenance and freshness rather than user-entered JSON.
3. Research game mechanics from documented sources and actual screens; store source, verification date, applicability and confidence. Do not fabricate minion rules or reset schedules.
4. Add bounded ADB actions with expected transitions, cancellation, no-op/popup detection, action journals and postcondition tests. Then implement one verified free-reward/navigation loop with controlled recovery.
5. Add persistent scheduler/checkpoints/resource policy and establish extended observation/agent soak tests before unattended gameplay claims.
6. Benchmark compact local VLMs only when they improve ambiguous-screen interpretation beyond measured OCR. See LOCAL_INFERENCE.md for the proposed matrix; no model download was needed this session.

## Architectural decisions

Use existing WinForms, built-in Windows OCR, pure .NET Core logic and a dependency-free regression runner. Initial persistence uses bounded, flushed atomic JSON records instead of introducing a SQLite package before account-wide transactions exist. Keep screenshot, account and OCR content local. Preserve public build-SHA updates and the separate updater. Treat synthetic values, heuristic scores and database memory honestly.

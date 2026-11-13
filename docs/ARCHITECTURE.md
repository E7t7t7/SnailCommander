# Architecture

## Implemented boundaries (v0.8.1)

- **Windows shell** (`src/SnailCommander`): WinForms dark UI; live capture and archive; connection/preferences; Intelligence, Minion Command and Updates pages. Partial MainForm files separate feature presentation.
- **ADB adapter** (`AdbService`): argument-list process execution, finite timeouts, binary PNG validation, 32 MB capture cap, cancelable screenshot operations and authorized-device parsing.
- **Windows observation adapter** (`Observation/ObservationService`): worker-thread image sampling and Windows OCR. Its independent semaphore prevents queued inference; cadence and perceptual changes govern analysis. Coordinates map back to the actual framebuffer.
- **Pure observation logic** (`SnailCommander.Core/Observation`): immutable structured records, conservative text screen rules, labeled resource parsing, independent cadence, atomic retained history and deterministic text replay.
- **Account evidence** (`SnailCommander.Core/Accounts`): partial labeled readings, unique minion-name binding, provenance/freshness/conflict projection and device/mode separation. `MainForm.AccountEvidence` renders and exports evidence; it never patches the optimizer's inventory automatically. See ACCOUNT_EVIDENCE.md.
- **Pure troop strategy** (`SnailCommander.Core/Minions`): evidence-tagged account models, effective stats, supplied mode/type/threshold bonuses, bounded composition search, explicit stat scoring and reserve-aware advisory cloning queues.
- **Pure update logic** (`SnailCommander.Core/Updates`): version policy, digest/package validation, durable file replacement journal and rollback, startup acknowledgment.
- **External update process** (`SnailCommander.Updater`): waits for app exit, applies staged package, verifies startup and restores on failure. Settings and account data live outside the installation.
- **Regression runner** (`tests/SnailCommander.Tests`): no test framework dependencies; failed assertions or exceptions yield failure exit code.
- **Windows integration runner** (`tests/SnailCommander.IntegrationTests`): real PNG/ADB plus Windows OCR, box bounds, persistence/replay and duplicate-screen suppression.
- **Profile paths** (`AppPaths`): startup-only `--data-root` configuration; default locations remain compatible. Custom profiles isolate captures and state, stay outside the installation, and propagate through updater restart/rollback.
- **Windows update process runner** (`tests/SnailCommander.UpdateIntegrationTests`): disposable installations, published real worker, fixture child processes and the real GUI; tests the OS process/file-lock/startup/rollback boundaries. `SnailCommander.UpdateProbe` is a test-only executable and is never packaged as the product.

- **Automation policy** (`SnailCommander.Core/Automation`): explicit input allowlist, live-source/freshness/device/package/target checks and conservative postconditions. `MainForm.Automation` orchestrates cancelable bounded sessions, bootstrap, preflight, input, verification and local checkpointing. `AutomationButtonReader` performs focused OCR without adding crops to account state. See AUTOMATION.md.

- **In-app game panel** (`MainForm.GamePanel`): a second bounded live picture view, manual tap/swipe/Back, source-age/device/foreground gates, capture/input serialization and cancellation handoff. `Core/Interaction/GameViewport` maps Zoom coordinates and rejects letterboxing. BlueStacks stays a separate process and may be minimized/restored through a uniquely selected native window; no cross-process reparenting is used.

## Current data flow

```text
ADB PNG ---> preview (1-10s) ---> archive (changed frame, >=10min)
    +----> observation gate ---> sampled change/cadence ---> Windows OCR (worker)
               +--> screen rules + resource parser ---> structured observation
                        +--> atomic local history + GUI evidence + text replay

Observed account JSON ---> validation/evidence ---> troop search ---> ranked explanations
                         +--> cloning request + reserves ---> advisory queue (core API)
```

Unknown screens and unreadable balances stay unknown. The observation stream is not yet an authoritative account model. Minion statistics are explicit account inputs; repeated OCR text is not silently converted into inventory.

## Persistence and concurrency

Observation records use immutable individual JSON files with atomic rename, disk flush, a cross-instance writer lock, record limits and bounded retention. Incomplete/corrupt records are skipped. This is an equivalent initial local store rather than SQLite; account-wide transactional relationships will need a database when scheduling/spending is implemented. JSON retention is intentionally finite and not seven days of every-frame recording.

Live capture, observation and update work have separate gates. OCR and optimization do not run synchronously on the GUI thread. Closing cancels pending observation/capture work; late UI callbacks check disposal. Window geometry is restored within an available working area; the app starts in a normal window.

## Next execution architecture

The intended loop remains OBSERVE -> UNDERSTAND -> UPDATE STATE -> GOALS -> OPTIONS -> PLAN -> EXECUTE -> VERIFY -> LEARN. The current release implements observation, a bounded understanding/state representation, offline composition evaluation, and persistence/replay. It does not implement the full loop.

Next: labeled real-screen fixtures and measured recognition coverage; verified account extraction; expand the first bounded ADB adapter with independently verified navigation routines; resource policy; durable task scheduling and measured recovery. OCR or a model response alone must never authorize a gameplay action. Retries must stop on unknown state and capture evidence.

Local model memory is not neural training. A local VLM should be added only after accuracy, latency and memory benchmarks show value beyond deterministic recognition/OCR. No paid APIs are part of this design.

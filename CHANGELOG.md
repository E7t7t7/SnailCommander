# Changelog

## 0.8.1 - 2026-11-13

- Add a full-height Game panel inside the GUI with local live mirroring, manual taps/drags, Android Back, and a minimize/restore BlueStacks button. BlueStacks can run minimized while the game is used inside Snail Commander.
- Map clicks through portrait/landscape letterboxing and ignore black-border clicks. Reject stale frames, changed devices and other foreground apps; serialize manual input with capture and require another frame before the next gesture.
- Stop and drain automation before enabling manual input; cancel/drain manual input before automation or update installation. Leaving Game disables manual control; closing restores a BlueStacks window minimized by this app.
- Add deterministic coordinate-mapping regression checks. The panel is a bounded ADB screenshot view, not native window reparenting or high-frame-rate video; keyboard/text/audio controls are not implemented.

## 0.8.0 - 2026-11-13

- Simplify navigation to Home, Settings, Advanced, Activity and Updates. Put Start automation, Pause/Resume, Stop and the current outcome on Home; keep an emergency Stop in the header. Preserve advanced tools and preview controls.
- Add one-button local BlueStacks connection/instance startup and fixed-package Super Snail launch.
- Add bounded, package/device/provenance/freshness-checked login, free reward acknowledgment and update-notice actions using exact OCR targets, focused button recognition, preflight observation, local action checkpoint and post-action verification.
- Cancel on Stop, close and device change; drain automation before installing updates. Never replay unfinished inputs or retry an unverified tap. Unsupported screens require attention.
- Add deterministic automation policies and recognition-only Windows integration diagnostics. Full gameplay and unattended scheduling remain future work.

## 0.7.0 - 2026-11-13

- Add an Account evidence page for partial labeled minion/capacity/resource readings, source text, timestamps, device/context, freshness, repeat observations and conflicts, with JSON export.
- Preserve explicit observation provenance. Imported images, replays, synthetic fixtures and legacy records cannot populate live account evidence; missing values remain unknown.
- Add strict labeled-field extraction with unique-name binding, numeric/quantity limits, rounded-display flags, ambiguity rejection and independent Leadership/Minion MAX fields. Keep optimizer inventory unchanged until further verification is implemented.
- Add bounded enlarged OCR fallback for Unknown screens, retaining native OCR for recognized screens after tests exposed enlargement regressions. Persist processing scale/pass diagnostics and keep one image's passes as one observation.
- Add deterministic account-policy tests and a local real-Windows-OCR synthetic fixture check; retain profile, update and rollback release checks.

## 0.6.2 - 2026-11-02

- Reopen the unchanged previous application if update backup creation fails before a transaction can be journaled.
- Add isolated local profiles with `--data-root`, including settings, captures, observations, account data, logs and update backups; preserve the profile through updater restart and rollback. Normal data locations remain unchanged.
- Add real Windows updater process tests for successful installation, failed startup, missing acknowledgment, exit during startup grace, mid-copy failure, backup creation failure, and recovery after terminating an updater. Exercise the actual GUI as an installed replacement in an isolated profile.
- Make the process tests and default/custom profile checks mandatory CI release gates. Keep startup-health timing bounded and clean up attempt-owned health files on failure.
- Derive displayed application versions from assembly metadata.

## 0.6.1 - 2026-11-01

- Fix installed-build detection when the .NET SDK duplicates the source revision in informational-version metadata. Prevent a same-build update loop.
- Publish explicit version/commit metadata without appending the revision twice; add regression coverage for real release metadata and conflicting revisions.

## 0.6.0 - 2026-11-01

- Add local Windows OCR with framebuffer text regions, conservative screen classification, heuristic evidence scores, labeled resource extraction and an independent changed-screen cadence.
- Persist bounded atomic observation records; inspect, export and replay them in the new Intelligence page.
- Add evidence-tagged minion/account models, independent Leadership and Minion MAX limits, ready/deployed/healing quantities, supplied threshold/mode/type bonuses, and bounded explainable composition search.
- Add Minion Command account import/edit/save, goal/mode selection and ranked troop comparisons. Synthetic examples remain explicitly labeled; scores do not predict battle wins or casualties.
- Add a reserve-aware advisory cloning queue in the core API.
- Strengthen public GitHub updates with version checks, release notes, periodic checks, optional verified download, bounded safe extraction, startup verification, persistent rollback journal and configurable backup retention.
- Run deterministic regression tests before CI release packaging; derive release versions from the application and guard Latest promotion against obsolete commits.
- Bound ADB capture size and GUI log buffers; retain window position/size and keep navigation reachable through scrolling.
- Add development guidance, architecture/status records and separate live Windows OCR/ADB integration checks.

## 0.5.1 - existing release

- Fix hidden update controls with explicit header columns and a permanent sidebar update action.
- Serialize Windows release publishing to reduce stale Latest updates.

## 0.5.0 - existing release

- Automatic live preview, configurable capture intervals, reconnect attempts and periodic changed-screen archives.

## 0.4.0 - existing release

- Dark navigation workspace, local screenshot history, connection settings and diagnostic activity views.

## Earlier versions

- ADB detection/capture and public release updating with an external replacement worker.

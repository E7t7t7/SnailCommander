# Development status

## Current session — 0.9.0 (2026-11-13 America/New_York)

Baseline: 5c5930c7d19c9c35973b2faea87209ab74649e9c, verified 0.8.1. Development remains in Games/SnailCommander-Development; release folders and normal user profiles are separate.

## Completed

- Extracted the real automation loop into Core/AutomationRunner and IAutomationPort, with deterministic simulated environments. Shared AdbAutomationPort is used by both GUI and opt-in live integration, including foreground/source rechecks and capture/input serialization.
- Added strict central login-error/Retry recognition, cooldown and reobservation. A per-device ledger reserves at most three recovery attempts per fifteen minutes before dispatch and survives restart. Malformed/oversized state fails closed; an exclusive profile lease prevents concurrent writers/sessions.
- Failed login is not counted as success. Explicit persistent login error can schedule bounded recovery; unknown outcomes cannot. Cancellation, limits or exceptions during dispatch/verification preserve uncertainty and never replay the old input.
- Added passive Home monitoring from three spatial task-board anchors, with ten-second checks. Sessions remain finite: ten minutes, 100 observations or twenty verified actions. Limits during verification require attention rather than reporting successful completion.
- Added latest run result and durable retry state, retaining the existing last-action checkpoint. No automatic restart, queued gesture replay, quantity ingestion or seven-day scheduler was introduced.
- Fixed startup readiness to wait for Android boot, since ADB can report connected before screenshot/game services are ready.
- Added bounded Retry button OCR and Home task-label font-width variants. A private local comparison found contrast thresholding degraded Home labels; raw colors were retained. One frame's variants are not independent confidence or neural training.

## Validation

- 265 deterministic assertions passed, including 35 new recovery/runner checks: exhausted/persisted budgets, cooldown reobservation, central-vs-chat Retry, wrong source/device/package, pause revision, storage failure, unknown outcomes, cancellation and interrupted verification limits.
- 8 local Windows OCR/persistence assertions passed using an explicitly rendered synthetic error dialog. Verified central Retry recognition in policy simulation, exclusion of synthetic inputs from production, persistence across reopen, single writer and corrupt-ledger rejection. No game input is sent by this fixture.
- First live attempt correctly stopped on an offline emulator with zero inputs. After bounded Android readiness, the production adapter verified a real update-notice dismissal and sent one login tap; the 45-second harness cancellation recorded that last input as unverified. It was not replayed on the next run.
- A subsequent live run sent zero inputs on an unsupported screen. Local inspection showed the game was Home and exposed the condensed-font OCR gap. After the measured crop changes, a live session recognized Home in three observations, monitored it and sent zero inputs. This is a small live sample, not an accuracy or unattended-duration benchmark.
- Actual login-error recovery has deterministic and real-OCR synthetic coverage; a successful real network-error recovery was not observed. No network fault was intentionally induced.
- Desktop capture was unavailable during GUI click testing. The isolated GUI built and started; existing real GUI installation/profile checks remain required release gates. Production ADB checks are reported separately from GUI click testing.
- Private screenshots/OCR, temporary benchmark code and all profiles are under ignored artifacts. No private account/chat content is part of publishing.

## Remaining work

Most gameplay remains unfinished: verified facility/resource collection, inventory ingestion with own-account identity, deployment, cloning/upgrading execution, comprehensive equipment/form/relic/partner/event planning, general disconnect recovery and seven-day scheduling/soak tests. Home monitoring intentionally does not click Done tiles. Next: establish a labeled own-account facility transition corpus and implement one free collection routine with source-specific expected outcomes. Maintain separate Leadership/Minion MAX and ready/deployed/healing quantities before deployment.

The in-app game view remains screenshot mirroring at roughly two frames per second, with manual tap/swipe/Android Back and minimize/show BlueStacks. Keyboard/audio/multi-touch are unfinished. Existing account-evidence, optimizer and updater limitations remain documented in their subsystem files.

## Release delivery

Local app/updater builds passed without warnings/errors. All 84 real updater-process assertions and 12 profile-path assertions passed. Public release verification pending. Existing versions and rollback ZIPs remain intact.

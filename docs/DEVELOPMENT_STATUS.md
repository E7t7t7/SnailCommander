# Development status

## Current session — 0.8.0 (2026-11-13)

Baseline public/local main: 2614c5e28d4e221ce6e09dc4da9f3380e061c68f, following the verified 0.7.0 release. No pre-existing worktree changes.

Implemented a simpler Home with Start automation, Pause/Resume, Stop, outcome text and live preview. Navigation is Home, Settings, Advanced, Activity and Updates. Existing account evidence, OCR, minion optimizer, captures and preview controls remain under Advanced. A header Stop is available across pages.

Start now connects to a loopback BlueStacks endpoint, starts the uniquely port-matched installed instance when possible, and opens the fixed Super Snail package. The first real input allowlist covers login Start, free reward OK and explicit update-notice dismissal. Every tap requires live-source/device/package/freshness/bounds/context checks, a second observation, an intent checkpoint and bounded post-action observation. Unknown state and unverified outcomes stop without repeating input. Pause blocks new actions; Stop/close/device changes cancel; updater installation drains the session. Unfinished checkpoints are never replayed. See AUTOMATION.md for exact boundaries and limits.

## Verification

- 212 deterministic assertions passed, including 34 added automation checks for source provenance, stale/future frames, wrong device/package, spending/consent contexts, ambiguity, bounds, login/notice signatures and postconditions.
- GUI builds passed without warnings/errors. App/updater are version 0.8.0.
- Windows profile checks passed all 12 assertions with disposable profiles; normal user data was not used for GUI/update tests.
- The real Windows updater process suite passed all 84 assertions locally and in final release CI. CI also passed all 212 deterministic and 12 profile checks (308 assertions total).
- Live local test: one Start connected BlueStacks and launched Super Snail from a different foreground game. Pause and Stop were exercised in the GUI.
- Live input test: one recognized notice-dismissal tap removed the notice. The initial postcondition stayed Unknown and correctly ended without retry. Recognition was then expanded to explicitly identify the login footer/Start transition, covered by a deterministic fixture.
- Live login test: one recognized Start tap reached a game Login verification failed/Retry dialog. The session correctly stopped with an unverified outcome; it did not retry, spend, or claim login success. Automatic recovery from that error is not implemented.
- Focused local OCR recognized the live notice and login button. This small sample is not an accuracy benchmark. The free reward branch has policy fixtures and is not claimed live-validated this session.
- Private real screenshots/OCR, synthetic fixtures generated during integration, logs and all test profiles stay in ignored artifacts. No private account/chat data is included in source publishing.

## Limits and next work

This is a bounded first automation slice, not a complete player. No facility navigation, arbitrary collection, deployment, cloning, upgrading, purchases, strategic gameplay scheduler, training, seven-day reliability or verified inventory ingestion is implemented. It will need manual help on most gameplay screens. Twelve unsupported observations or an unverified input end the run; twenty verified actions is the session cap.

Next priorities: recognize and test the login-failure recovery transition with a strict retry budget; gather labeled private fixtures for successful login/home/reward transitions; add a fake-environment execution harness and interrupted-input recovery tests; then add one verified facility routine at a time. Continue own-account identity and minion-detail validation before inventory ingestion or troop deployment. OCR and heuristic scores remain fallible; local persistence is not neural training.

Updater replacement/rollback, public token-free downloads and SHA-256 validation are preserved. Manual rollback selection, global archive/log cleanup and a seven-day soak remain incomplete. Prior 0.7.0 evidence extraction and advisory optimizer limits still apply; see ACCOUNT_EVIDENCE.md and MINION_OPTIMIZER.md.

## Release delivery

- Published code commit: 104ef5e981efa1ddd9c60f000c79a222663459d1.
- Public release: [v0.8.0](https://github.com/E7t7t7/SnailCommander/releases/tag/build-104ef5e981efa1ddd9c60f000c79a222663459d1).
- [CI 43463393665](https://github.com/E7t7t7/SnailCommander/actions/runs/43463393665): both build and publish-release succeeded; 308 assertions passed before packaging.
- Downloaded the actual public ZIP, 102007051 bytes, and matched GitHub SHA-256: 5ef063b90b93d2fd00b9c2af1bec4239dee409c77050a465efeae06a620248f4.
- Extracted beside the source checkout to SnailCommander-v0.8.0/SnailCommander.exe; ZIP retained as SnailCommander-v0.8.0.zip. Existing versions and normal profile data remain intact.
- Verified executable ProductVersion exactly 0.8.0+104ef5e981efa1ddd9c60f000c79a222663459d1.

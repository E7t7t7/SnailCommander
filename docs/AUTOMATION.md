# Bounded automation (0.8.0)

Home has Start automation, Pause/Resume, and Stop. A second Stop remains in the header on every page. Settings contains connection details; Advanced retains Account, Intelligence, Minion Command, captures and preview controls. Preview pause is independent of automation pause.

Start discovers the installed ADB executable, connects to the configured loopback endpoint, and opens Super Snail (`com.qcplay.snail.android.na`). If BlueStacks is stopped, startup matches its configured instance to that port before launching it, with a bounded connection loop. An ambiguous/missing instance requires attention. Other emulators and non-loopback endpoints are not automatically bootstrapped. USB devices are not selected by automation.

The first input allowlist is deliberately small:

- Login: an exact Start word/button with server/version context or a bottom-of-frame Ver: footer, with the game in the foreground.
- Reward acknowledgment: exact OK with Rewards Gained or Obtain 1 body context.
- Update notice dismissal: exact Tap with explicit close notice text and maintenance/update context.

All targets come from current OCR regions. Pale/orange connected components propose button crops; color alone never authorizes a tap. Bounded footer/modal crops and contrast variants improve stylized text recognition. Full-frame observations alone enter account history; focused crops are not authoritative account readings. Everything runs locally using Windows OCR.

Core/Automation contains the pure source, freshness, bounds, context and postcondition policy. MainForm.Automation owns the cancelable session and Windows/ADB orchestration. A session is bound to one serial, checks the foreground package, reobserves before dispatch, and records an action intent before input. It observes the result up to three times, without resending the tap. Unknown pixels and ADB success are not completion. A changed known screen, or the explicit Notice-to-Login signature, is required; the original context must disappear.

Pause gates new actions; a tap already dispatched may finish verification. Resume reobserves if pause interrupted a preflight. Stop cancels ADB/OCR/delays. Device changes cancel the run, closing cancels it, and update installation drains it before replacement. No second session starts while one is stopping. Startup never replays an unfinished input from the journal.

Twelve unsupported observations end the run with Needs attention; at least five seconds separates unsupported observations. A session also stops after 20 verified inputs. A failed preflight never sends a tap. An unverified outcome ends the run without retry. Local `Automation/last-action.json` stores the latest intent/sent/verified/unverified checkpoint, not a durable queue. Session logs and observation history provide supporting evidence. Stop/crash may leave an intent or sent checkpoint; it is never assumed complete.

This is not a complete autonomous player or seven-day scheduler. It does not navigate facilities, deploy troops, collect arbitrary resources, clone, upgrade, plan events, solve challenges, spend currency, or purchase anything. Unsupported screens require manual intervention and another Start. OCR coverage remains limited and no accuracy percentage or unattended-duration claim is made.

Validation: deterministic policy fixtures are synthetic. The integration runner supports `--check-automation-recognition` with either its existing `--adb`/`--device` or `--image` inputs. It performs local OCR and writes a local report without input; imported files remain ineligible to act. Real game screenshots and OCR reports remain ignored under artifacts and must not be published.

# Snail Commander engineering instructions

## Product and architecture

- Evolve the existing .NET 8 Windows Forms application; preserve ADB capture, screenshot history, preferences, and public GitHub updating.
- Use distinct observation, recognition, state, strategy, execution, verification, and persistence components. Keep platform-independent logic in `SnailCommander.Core`; Windows adapters and UI belong in `SnailCommander`.
- Local processing only: no paid inference APIs or recurring subscriptions. Screenshot/account data stays on this computer.
- Never claim autonomous gameplay, neural training, real combat simulation, or seven-day reliability until implemented and measured. Database memory is not model training.
- Unknown information must remain unknown. Attach source/evidence and confidence to mechanics and account inputs. Synthetic fixtures must be clearly labeled and never treated as the player's account.
- Leadership limits total deployment; Minion MAX limits an individual type. Model available, deployed, and healing quantities independently.
- Future gameplay actions must be bounded, cancelable, state-checked and followed by observation/verification. No blind retry loops, real-money purchases, or premium spending without explicit authorization.

## Engineering and verification

- Preserve user changes. Prefer small, coherent additions over rewrites. Keep expensive OCR/optimization off the GUI thread, throttle work and bound storage.
- Run `dotnet build src/SnailCommander/SnailCommander.csproj -c Release`, the regression runner at `tests/SnailCommander.Tests`, and the updater build before publishing.
- Add deterministic tests for new decision logic and failure/recovery behavior; replay fixtures should avoid reliance on a live emulator.
- Live BlueStacks/OCR testing must be reported separately from fixture tests. Never claim emulator integration if it was unavailable.
- Release versions, assembly metadata, changelog and release title must agree. Preserve `build-<40 character commit SHA>` tag compatibility with existing clients.
- Public release downloads must need no token, verify SHA-256 before staging, preserve local data, run an external updater, and support rollback on failed startup.
- CI must run regression tests before packaging and never mark an obsolete commit as Latest.
- Keep `docs/DEVELOPMENT_STATUS.md`, `docs/ARCHITECTURE.md` and `CHANGELOG.md` accurate at the end of each session. Record specific next steps and limitations.

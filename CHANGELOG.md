# Changelog

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

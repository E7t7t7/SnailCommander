# GitHub releases and updater

The Windows application downloads public releases from `E7t7t7/SnailCommander` without a GitHub login or personal access token. The GitHub Actions publisher uses the repository's temporary `GITHUB_TOKEN`; this credential is not distributed with the application.

## Release contract

- The release tag is `build-` followed by the exact 40-character hexadecimal source commit. This preserves compatibility with existing installed versions.
- The release title is `Snail Commander vX.Y.Z (build COMMIT_PREFIX)`. CI reads `X.Y.Z` from `src/SnailCommander/SnailCommander.csproj` and sets application informational version to `X.Y.Z+FULL_COMMIT`.
- The required asset is `SnailCommander-win-x64.zip`. Its root contains `SnailCommander.exe` and `SnailCommander.Updater.exe`, both published for `win-x64` as self-contained single-file executables. Other runtime dependencies produced by publishing remain in the package.
- A separate `SHA256SUMS.txt` asset is published for manual verification. The application verifies the ZIP against the **release asset's GitHub API `digest` field**, in `sha256:64_HEX_DIGITS` format. It does not use the checksum text asset as its trust source. Missing, malformed, or mismatched digests prevent installation.
- The application queries `/repos/E7t7t7/SnailCommander/releases/latest`, rejects drafts and prereleases, and accepts download links only under this repository's HTTPS GitHub release-download prefix. Release notes are displayed in Updates.

Exact commit matches are already installed. A release with a parsed version below the installed assembly version is rejected as a downgrade; a different commit with the same version remains eligible. Legacy release titles without a version remain supported, so their semantic version cannot be compared. The updater does not perform commit-ancestry comparisons.

## Package and filesystem limits

The downloaded ZIP is limited to `400 * 1024 * 1024` bytes (400 MiB). Expanded declared content is limited to `1200 * 1024 * 1024` bytes (1,200 MiB), with at most 4,096 ZIP entries. Validation runs before extraction and rejects traversal, rooted paths, Windows reserved names, invalid path segments, case-insensitive duplicate paths, and symbolic-link entries. Both executables must exist at the package root.

The installer validates the staged tree again before replacing application files: at most 4,096 filesystem entries, 64 directory levels, and 1,200 MiB of files. Staging, destination, and backup directories must not overlap. Reparse points such as symbolic links and junctions are rejected in staged entries and the destination/backup path components used for replacement or restoration. These checks are filesystem safeguards, not a guarantee against another process modifying paths concurrently.

## Installation, health, and rollback

1. The application stages the verified archive under `%TEMP%\SnailCommander\Update-<GUID>\staging`. It copies the staged updater to a sibling temporary worker executable, starts that worker, and closes.
2. The separate worker takes a local update mutex and waits up to 90 seconds for the supplied application process to exit. It never overwrites a running application's files merely because that deadline expires.
3. It creates `%LOCALAPPDATA%\SnailCommander\Backups\<UTC_TIMESTAMP>-<ID>`. `journal.json` records the installation directory, old/new product-version metadata, transaction status, and each affected relative filename. Original files are copied beneath `files\` in the backup. The rollback intent is saved by temporary-file replacement before each destination copy.
4. After copying, the journal enters `awaiting-health`. The worker launches the new application with a unique health filename and nonce. Once the main form is shown, the application acknowledges that nonce atomically beneath `%TEMP%\SnailCommander`. The worker waits up to 45 seconds for this acknowledgment, then requires the process to remain alive for three more seconds before marking the transaction `healthy`.
5. A copy failure, launch failure, early process exit, or missing acknowledgment triggers rollback while the worker remains running. It stops only its own replacement process, restores overwritten files in reverse order, removes newly introduced files, marks the journal `rolled-back`, and attempts to reopen the previous installation. Restoration failures are logged and preserve the backup for investigation.

The startup check confirms that the form opened and the process survived the short grace period. It does not establish OCR accuracy, ADB availability, successful gameplay, or long-term stability. Journal replacement improves recoverability but is not a whole-directory atomic installation or a promise of power-loss durability.

If the worker itself is killed, automatic rollback does **not** continue independently. On the next updater attempt, after the old application exits, the worker scans journals for the same installation and restores transactions left in `installing` or `awaiting-health` before beginning the new installation. If the application cannot start enough to request another update, manual recovery is required. Preserve `journal.json`, its `files\` contents, and `%LOCALAPPDATA%\SnailCommander\Logs\updater.log` when investigating such a failure.

## Data preservation and retention

Settings, account JSON, observation history, and logs live beneath `%LOCALAPPDATA%\SnailCommander`; screenshots and capture metadata live beneath the Windows Pictures folder's `SnailCommander\Captures`. Normal package replacement targets the application installation directory, so these data stores are preserved. Files unrelated to the package are left in the installation directory. Personal files sharing a packaged filename there can be replaced; application updates are not backups of arbitrary personal data or future database migrations.

Updates exposes a saved backup-retention preference from one to ten previous installations; the default is three. After a healthy startup, retention prunes older backups whose journals are marked `healthy`. Failed/interrupted transaction evidence and legacy backups without compatible journals are deliberately not included in automatic pruning, so those directories can accumulate. Backups are affected-file snapshots with journal metadata, not complete interchangeable installation ZIPs.

The **Open update history** button opens the backup folder. A selectable rollback-version dropdown and a complete user-facing manual rollback operation are not implemented yet. Do not present that button as an automatic restore facility.

Temporary downloads, extracted staging directories, copied workers, and failed health files can remain after attempts. Comprehensive temporary-storage cleanup is outstanding. Do not delete a staging directory while its update worker is active.

## CI publication and validation limits

The workflow runs the console regression suite before publishing either executable; a failing test or publish command prevents the release job. It checks both executables exist, uploads the build artifact with missing files treated as an error, packages the release, and adds the current changelog entry to release notes. Pull requests build without publishing a public release.

Release publication is serialized. The job checks that its commit is still `main` before creating a release, creates it with `--latest=false`, and rechecks `main` immediately before promoting Latest. Reruns repair existing release assets and metadata before promotion. These checks prevent routine stale-build reruns from replacing a newer release, but the branch check and GitHub's Latest update are separate API operations: a concurrent branch change or external release publisher creates a narrow race. This is not an atomic compare-and-set on repository state.

The development session passed 108 regression assertions, including digest rejection, package traversal/duplicate-path rejection, version comparison, journal installation/restoration, simulated mid-copy failure, retention, directory overlap, and health-nonce acknowledgment. These deterministic checks do not constitute a real installed-application update/rollback test. End-to-end updater installation, deliberate startup-failure recovery, and killed-worker recovery still need isolated integration validation.

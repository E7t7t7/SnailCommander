# Snail Commander

Local-first Windows companion for observing **Super Snail** in **BlueStacks**.

## v0.3.0 — Token-free updates (public GitHub repository)

- Detect BlueStacks' ADB, connect to the emulator, list devices, and capture screenshots.
- Dark Windows Forms diagnostic dashboard with local screenshot preview and logs.
- **Update from GitHub** button requires **no Personal Access Token**.
- Checks the [latest public GitHub Release](https://github.com/E7t7t7/SnailCommander/releases/latest).
- Downloads the release ZIP, verifies its GitHub SHA-256 asset digest, and checks expected application files.
- Launches a separate installer after the app closes, backs up replaced files, and reopens the app.
- A button is included to delete any **legacy private-repo updater token** from Windows Credential Manager.

**This remains a read-only diagnostics tool**, not a gameplay bot. It neither taps the game nor spends resources. Autonomous gameplay would require separate discussion of reliability and Super Snail's rules.

## Upgrade from v0.1 or v0.2 (one-time manual step)

Your previously installed application still contains the old private-repo updater, so it cannot update itself to this token-free architecture.

1. Open the [Snail Commander Releases page](https://github.com/E7t7t7/SnailCommander/releases/latest).
2. Download **SnailCommander-win-x64.zip** under **Assets**.
3. Close the old Snail Commander application.
4. Extract the new ZIP into your existing **writable** Snail Commander application folder, replacing existing application files.
5. Launch **SnailCommander.exe**.

**After this manual upgrade:** Click **Update from GitHub** in the application for future releases. No GitHub account, personal access token, or separate subscription is required to download public builds.

If you previously generated a fine-grained Personal Access Token for the private repository, use **Remove old GitHub token** in the app to delete the locally stored credential, and [revoke the token on GitHub](https://github.com/settings/personal-access-tokens) since it is no longer needed.

## How public updates are published

Pushing to `main` triggers GitHub Actions. After a successful Windows build, the workflow creates a public GitHub Release named with the full commit SHA (`build-<40-character-sha>`) and attaches the downloadable Windows ZIP.

The app compares its embedded build SHA with the latest published release, verifies SHA-256 of the downloaded package, then installs using an external worker. Existing files are backed up under `%LOCALAPPDATA%\SnailCommander\Backups`. Update logs are under `%LOCALAPPDATA%\SnailCommander\Logs\updater.log`.

Do not store passwords, screenshots or access tokens in this **public** repository.

## BlueStacks ADB setup

1. BlueStacks Settings → Advanced → enable Android Debug Bridge.
2. Launch BlueStacks with Super Snail open.
3. In Snail Commander, **Detect ADB**, enter the emulator's port (commonly `127.0.0.1:5555`), then **Connect**.
4. **Refresh devices** and select the correct BlueStacks entry.
5. **Capture screenshot**. The screenshot will appear in the dashboard, and the file will be saved to `Pictures\SnailCommander\Captures`.

Your 960 × 540 BlueStacks window does not necessarily equal the Android display resolution.

## Development

- .NET 8, WinForms, self-contained Windows x64 release
- BlueStacks bundled `HD-Adb.exe` or Android SDK `adb.exe`
- GitHub Actions release pipeline with limited `contents: write` permission only for publishing

## Next milestones

- Read-only screen classification, OCR testing, and local vision-model experiments
- Saved account observations and decision-support recommendations
- Weekly progress comparison reports
- Error and recovery diagnostics

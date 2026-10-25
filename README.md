# Snail Commander

A local-first **Windows companion** for Super Snail running inside BlueStacks.

## v0.4.0 — Command dashboard redesign

The v0.4 release replaces the cramped original diagnostics screen with a dark navigation workspace:

- **Overview:** large game screenshot preview, connection status, current capture resolution, fast Capture Screen button, and recent activity log.
- **Captures:** local screenshot history with timestamps, device details, resolution and manual screen-type labels (Home, Story/Memory, Exploration, Battle, Equipment, Relics, Research, Event, Other).
- **Connection:** auto-detect ADB on app launch, reconnect BlueStacks, switch between available Android device serials, adjust local endpoint, and remember preferred ADB executable and device.
- **Activity:** persistent diagnostic logs and accessible log folder.
- **Update GitHub:** one-click token-free updates using public GitHub Releases. The download is verified against the release asset SHA-256 digest before installation. A separate update worker closes/reopens the app and backs up overwritten files.

**Important:** The application remains **read-only**. It does not tap game controls, manage resources, or play unattended. Saved screen labels are provided by the user—they are not automatically inferred by an AI model yet.

## Install and update

[Download latest release](https://github.com/E7t7t7/SnailCommander/releases/latest)

If using v0.3 already, select **Update GitHub** inside the app. After confirming, the update will download, be verified and installed through a separate worker, and the app will reopen. If the app update has any issue, install the release manually:

1. Download **SnailCommander-win-x64.zip** from the latest release's Assets.
2. Close Snail Commander.
3. Extract the ZIP into your existing **writable** application folder, replacing the program files.
4. Run **SnailCommander.exe**.

No GitHub login, token, .NET runtime or additional AI subscription is required to download and run the self-contained Windows release. Do not run the app directly from the ZIP.

**If you previously generated a token for the old private repository**, use Connection → Remove legacy GitHub token, then [revoke the old token on GitHub](https://github.com/settings/personal-access-tokens).

## Connect BlueStacks

1. Run BlueStacks with Super Snail open. In BlueStacks Settings → Advanced, enable **Android Debug Bridge**.
2. Start Snail Commander. It will try to detect and connect to a local ADB endpoint automatically.
3. If it does not connect, go to **Connection**, select the local ADB executable (typically HD-Adb.exe), enter the BlueStacks port (e.g. 127.0.0.1:5555), and click Connect.
4. Select the correct authorized emulator device in the dropdown.
5. Go to Overview → **Capture screen**.
6. Find saved screenshots under your Windows **Pictures/SnailCommander/Captures** folder. Select a file in **Captures** and optionally label the kind of screen.

The desktop emulator window size can differ from the Android framebuffer resolution. Snail Commander reports the actual captured dimensions automatically. It supports portrait and landscape captures.

Screenshots, labels, connection settings, and logs stay on your Windows computer, in your Pictures and %LOCALAPPDATA%/SnailCommander folders. They are not uploaded to GitHub.

## Notes and limitations

- Capture classification requires manual labels; image text/OCR is not implemented yet.
- ADB permission and emulator availability depend on BlueStacks settings.
- Week-long unattended gameplay is **not** implemented or verified.
- Super Snail's published rules restrict gameplay automation; using such automation could risk the account.

## Development

.NET 8 / WinForms, self-contained Windows x64 publish. Public release builds use GitHub Actions.

- `src/SnailCommander/MainForm.cs` — desktop workspace and navigation
- `src/SnailCommander/UiKit.cs` — reusable dark UI controls
- `src/SnailCommander/CaptureStore.cs` — metadata, labels and local capture history
- `src/SnailCommander/AdbService.cs` — ADB read-only diagnostics and image capture
- `src/SnailCommander/GitHubUpdates.cs` — public release updater with SHA-256 validation
- `src/SnailCommander.Updater/` — separate update worker and rollback

## Next milestones

- Read-only screen recognition and OCR evaluation
- Optional local vision-language model compatibility
- Account progression analysis and explainable recommendations
- Saved snapshot comparisons and weekly reports

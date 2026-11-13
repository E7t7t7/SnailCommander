# Snail Commander

Local Windows command center for Super Snail in BlueStacks. **v0.8.1 adds an interactive Game panel inside the GUI.** It includes local OCR, account evidence, troop planning, isolated profiles and process-tested public updating.

[Download the latest Windows release](https://github.com/E7t7t7/SnailCommander/releases/latest)

## Use

1. Extract `SnailCommander-win-x64.zip` into a writable folder and run `SnailCommander.exe`. The self-contained release includes .NET; no GitHub token or AI subscription is needed.
2. On **Home**, press **Start automation**. It connects to the configured BlueStacks instance, starts it when its local configuration can be matched, and opens Super Snail. ADB must be enabled in BlueStacks. Adjust unusual installations under **Settings**.
3. Open **Game** (or **Game panel** in the header), enable manual control, and click/drag directly in the live game image. **Minimize / show BlueStacks** keeps the emulator out of the way. It continues running in the background. The view refreshes at roughly two frames per second, not video frame rates. Manual control stops automation; switching away disables manual input.
4. Use **Pause/Resume** to suspend new actions or **Stop** to cancel. Stop is also available in the header on every page.
5. This first automation release handles recognized login Start, free reward OK, and update notices. It rechecks the screen before input and verifies the result. Unknown screens or unverified outcomes end with **Needs attention**, without repeating the tap. Continue manually and press Start again.
6. **Advanced** retains Account evidence, Intelligence/OCR, Minion Command, captures and preview controls. Troop optimization remains advisory and uses your local account JSON. Synthetic examples are demonstration data.
7. **Updates** checks public releases and installs verified packages with external replacement and rollback. No token or subscription is required.

**It does not run every game system yet.** Facility navigation, deployment, cloning, upgrades, spending, persistent scheduling and seven-day operation remain unfinished. See [automation behavior and limits](docs/AUTOMATION.md). Partial account evidence does not automatically populate optimizer inventory; see [account evidence](docs/ACCOUNT_EVIDENCE.md).

## Local data

- Screenshots and manual labels: Windows Pictures directory, `SnailCommander/Captures`.
- Settings, logs, observation history, account JSON and update backups: `%LOCALAPPDATA%/SnailCommander`.
- Observations retain up to 3,000 records (512 KB per record limit). Images are not embedded in observation JSON.
- No screenshots, account inputs or OCR text are sent to cloud inference. Only public release metadata and package downloads use GitHub.
- Windows OCR requires an installed supported OCR language. English rules work best with the English game UI.

Existing v0.5.1 installations can use **Update from GitHub**. If an older build has no visible update button, download and extract the latest release once.

For a separate account or a disposable test installation, launch `SnailCommander.exe --data-root "C:\SnailProfiles\AccountTwo"`. Use a directory outside the application installation. All profile data, including captures and updater backups, then lives beneath that directory; the profile name appears in the title. Without this option, existing data locations remain unchanged. Updates and rollback preserve the selected profile. Profiles do not automatically discover another account's state.

## Development

Requires .NET 8 SDK on Windows. Restore downloads the Windows SDK projections used by built-in OCR; no third-party inference package is required.

```powershell
dotnet build src/SnailCommander/SnailCommander.csproj -c Release
dotnet build src/SnailCommander.Updater/SnailCommander.Updater.csproj -c Release
dotnet run --project tests/SnailCommander.Tests/SnailCommander.Tests.csproj -c Release
```

The regression runner exits nonzero on any failed assertion. CI runs it before packaging and publishing. Windows OCR/ADB integration is explicit and separate:

```powershell
dotnet run --project tests/SnailCommander.IntegrationTests -c Release -- --check-profiles --output artifacts\profiles
dotnet run --project tests/SnailCommander.IntegrationTests -c Release -- --image C:\path\screenshot.png --output artifacts\ocr
dotnet run --project tests/SnailCommander.IntegrationTests -c Release -- --adb "C:\Program Files\BlueStacks_nxt\HD-Adb.exe" --device 127.0.0.1:5555 --output artifacts\live
```

The release workflow also runs the actual updater executable against disposable child processes and the published GUI. See [updater process validation](docs/UPDATER.md) for the reproducible commands and the distinction between process fixtures and live gameplay.

See [development status](docs/DEVELOPMENT_STATUS.md), [architecture](docs/ARCHITECTURE.md), [observation](docs/OBSERVATION.md), [troop optimizer](docs/MINION_OPTIMIZER.md), [local inference](docs/LOCAL_INFERENCE.md), and [updater](docs/UPDATER.md).

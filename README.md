# Snail Commander

Local Windows command center for Super Snail in BlueStacks. **v0.6.2 strengthens update recovery with real Windows process tests and isolated local profiles.** It includes local OCR, persistent structured observations and a bounded troop composition optimizer, while preserving the live preview and public GitHub updater.

[Download the latest Windows release](https://github.com/E7t7t7/SnailCommander/releases/latest)

## Use

1. Extract `SnailCommander-win-x64.zip` into a writable folder and run `SnailCommander.exe`. The self-contained release includes .NET; no GitHub token or AI subscription is needed.
2. Start BlueStacks with Android Debug Bridge enabled. Snail Commander detects its ADB executable and tries `127.0.0.1:5555`. Change the path, endpoint or selected emulator in **Connection** if needed.
3. **Overview** shows automatic live capture. Dimensions come from the Android framebuffer; landscape launcher and portrait game screens can differ. Preview runs every 1, 2, 5 or 10 seconds.
4. **Intelligence** shows OCR text, detected screen, supporting phrases, text boxes and conservatively labeled resource readings. Changed screens are analyzed at most every 10 seconds, with a refresh every 60 seconds for small changes. Analyze the current screen or an image file, inspect saved observations, replay text through current rules, or export JSON.
5. **Minion Command** imports/edits a local account JSON and calculates ranked compositions for different goals and modes. `samples/minions.empty-account.json` is the starting format. **Load synthetic example** uses invented demonstration data and does not represent your account.
6. **Updates** checks public releases at startup and every six hours, displays notes, and installs a verified download when selected. Optional automatic downloading never installs without the install action. Failed startup triggers rollback through a separate updater. Backup retention is configurable from 1-10 installations.

The application currently observes and analyzes. It does **not** navigate game menus, deploy troops, spend resources, or play unattended. Automatic account/minion extraction, validated combat formulas, local vision-language inference, and seven-day operation remain future work. Rule scores are heuristics, not calibrated probabilities. OCR can misread stylized game text.

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

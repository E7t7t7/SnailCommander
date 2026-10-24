# Snail Commander

A **local-first Windows companion** for inspecting and planning progress in *Super Snail* running in BlueStacks.

## Current status: v0.1.0 — read-only connection diagnostics

- Detect a BlueStacks/Android ADB executable.
- Connect to a local emulator ADB endpoint.
- List connected emulator devices.
- Capture the current emulator display to a PNG.
- Preview screenshots in a dark Windows dashboard.
- Keep local diagnostics logs.

**Not implemented:** gameplay automation, autonomous decisions, account analysis, local vision model, upgrades, or weekly progress reports. The initial milestone intentionally **does not control the game or spend anything**.

> Super Snail's terms may prohibit bots, macros, and automated gameplay. Review the publisher's current rules before considering anything beyond an observational/strategy companion.

## Download or run

**No SDK route:** Open this repository's **Actions** tab, select the latest successful **Windows build**, and download the `SnailCommander-win-x64` build artifact ZIP. Extract the ZIP and launch `SnailCommander.exe`. The workflow builds a self-contained Windows executable.

**Developer route:** Install the .NET 8 SDK on Windows and run:

```powershell
git clone https://github.com/E7t7t7/SnailCommander.git
cd SnailCommander
dotnet run --project src/SnailCommander/SnailCommander.csproj
```

## Connection checklist

1. Launch BlueStacks and sign in to Super Snail.
2. BlueStacks Settings → Advanced → **Android Debug Bridge** → enable.
3. Start Snail Commander, choose **Detect ADB**, and confirm the path.
4. Enter the local ADB endpoint shown by BlueStacks (commonly `127.0.0.1:5555`).
5. Select **Connect**, then **Refresh devices**.
6. Choose the device and select **Capture screenshot**.
7. Open Super Snail itself and capture again for useful account analysis.

Captures are stored under your Windows **Pictures/SnailCommander/Captures** folder. The app does **not** upload screenshots or account information.

If `HD-Adb.exe` cannot be found, browse for the executable under your BlueStacks installation, or use the standard Android SDK `adb.exe`.

## Roadmap

- [x] Read-only ADB diagnostics and screenshot capture
- [ ] Robust device/reconnection diagnostics
- [ ] Screen classification and OCR validation
- [ ] Local reference knowledge and progression planner
- [ ] Optional local vision-language model
- [ ] Persistent local observations, progress comparisons, and weekly summaries
- [ ] Human-reviewed strategy advice and permitted workflows

## Tech stack

- C# / .NET 8 / Windows Forms; no third-party UI dependencies
- ADB for read-only emulator status and screen capture
- GitHub Actions for Windows x64 ZIP builds

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for boundaries, planned modules, and reliability expectations.

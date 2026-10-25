# Snail Commander

A local-first **Windows companion** for inspecting and planning progress in Super Snail running inside BlueStacks.

## v0.2.0: Read-only diagnostics + one-click GitHub updates

- Detect HD-Adb.exe or adb.exe, connect to BlueStacks, and enumerate connected devices.
- Save and preview screenshots in Pictures/SnailCommander/Captures.
- Record diagnostics under %LOCALAPPDATA%/SnailCommander/Logs.
- **Check & install update** downloads the latest successful compiled build from this **private repository**.
- A separate updater waits until the app closes before replacing its files and restarting it.
- Existing files are backed up under %LOCALAPPDATA%/SnailCommander/Backups.
- GitHub access token is saved to **Windows Credential Manager**, never to plain-text configuration or log files.

**Not implemented:** autonomous gameplay, tapping, resource spending, local vision models, or progress reports. This version only observes the emulator.

## Install v0.2.0 (one time)

1. Visit [GitHub Actions](https://github.com/E7t7t7/SnailCommander/actions/workflows/windows-build.yml).
2. Select the latest **successful** Windows build from the main branch.
3. Under **Artifacts**, download **SnailCommander-win-x64**.
4. Extract the ZIP into a writable folder such as Desktop/SnailCommander. Do not launch from inside the ZIP and avoid Program Files.
5. Run **SnailCommander.exe** with BlueStacks running.

Your existing v0.1.0 **does not contain the updater**, so v0.2.0 must be downloaded manually once. Subsequent updates can use the in-app button.

## Configure the private-repo updater (once)

1. On GitHub, open **Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token**.
2. Set **Repository access → Only select repositories → SnailCommander**.
3. Grant **Actions: Read-only** under repository permissions. No write permissions are needed.
4. Generate the token. Do **not** share it in ChatGPT or screenshots.
5. In Snail Commander, scroll the left panel to **GITHUB UPDATES (PRIVATE REPO)**.
6. Paste the token into the masked field and click **Save token**.
7. For future updates, click **Check & install update**. Confirm the prompt; the app will download, close, update, and reopen.

Use **Remove token** to delete the credential. If a token expires or is revoked, generate and save a replacement. Do not create an all-repositories token.

### How the updater works

- Reads successful GitHub Actions runs from the repository's main branch.
- Compares the current app's embedded Git commit to the latest successful workflow.
- Downloads the matching Windows artifact through GitHub's authenticated API.
- Checks for expected app and updater executables in the extracted archive.
- Runs an external worker from a temporary folder to avoid Windows file-in-use locks.
- Backs up replaced files and attempts rollback on errors.
- Relaunches the application after installation.

If updates fail, review **%LOCALAPPDATA%/SnailCommander/Logs/updater.log**.

## BlueStacks connection

1. BlueStacks Settings → Advanced → turn **Android Debug Bridge** ON.
2. In Snail Commander click **Detect ADB**, then **Connect** to the port shown by BlueStacks, commonly 127.0.0.1:5555.
3. Click **Refresh devices**, choose the matching emulator, and click **Capture screenshot**.
4. The screenshot's actual resolution is detected independently of the size of the BlueStacks window.

## Local development

Requires Windows and .NET 8 SDK.

~~~powershell
dotnet publish src/SnailCommander/SnailCommander.csproj -c Release -r win-x64 --self-contained true
dotnet publish src/SnailCommander.Updater/SnailCommander.Updater.csproj -c Release -r win-x64 --self-contained true
~~~

GitHub Actions creates a self-contained Windows build so end users need no .NET SDK.

## Next milestones

- Image recognition and OCR quality checks
- Optional offline vision model on compatible GPUs
- Confidence-scored progression analysis and recommendations
- Local snapshots and weekly progress reports

Super Snail's published terms restrict automated gameplay. The current app does not automate gameplay.

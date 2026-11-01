using System.IO.Compression;
using System.Security.Cryptography;
using SnailCommander.Core.Updates;

public static class UpdateTests
{
    public static void Run(Action<bool, string> assert)
    {
        string first = new('a', 40), second = new('b', 40);
        assert(UpdatePolicy.CommitFromTag("build-" + first) == first, "Update build tag accepts full commit");
        assert(Throws<InvalidDataException>(() => UpdatePolicy.CommitFromTag("build-abc")), "Update rejects malformed build tag");
        assert(!UpdatePolicy.IsUpdateAvailable(first, new Version(0, 6, 0), first, new Version(0, 6, 0)), "Exact installed build is not an update");
        assert(!UpdatePolicy.IsUpdateAvailable(first, new Version(0, 6, 0), second, new Version(0, 5, 1)), "Older release cannot downgrade application");
        assert(UpdatePolicy.IsUpdateAvailable(first, new Version(0, 6, 0, 0), second, new Version(0, 6, 0)), "Same-version new commit remains updateable");
        assert(UpdatePolicy.IsUpdateAvailable(first, new Version(0, 5, 1), second, new Version(0, 6, 0)), "Newer version is updateable");
        assert(UpdatePolicy.VersionFromReleaseName("Snail Commander v0.6.0 (build abcd)") == new Version(0, 6, 0), "Version derives from release metadata");
        assert(UpdatePolicy.VersionFromReleaseName("Snail Commander (build abcd)") is null, "Legacy release names remain supported");
        var healthToken = Guid.NewGuid().ToString("N");
        var healthDirectory = Path.Combine(Path.GetTempPath(), "SnailCommander");
        Directory.CreateDirectory(healthDirectory);
        var healthPath = Path.Combine(healthDirectory, "health-" + healthToken + ".ok");
        try
        {
            StartupHealth.Acknowledge(new[] { "--update-health", healthPath, "--update-token", "invalid-token" });
            assert(!File.Exists(healthPath), "Startup health rejects an invalid token");
            StartupHealth.Acknowledge(new[] { "--update-health", healthPath, "--update-token", healthToken });
            assert(File.ReadAllText(healthPath) == healthToken, "GUI startup acknowledgment round trip retains exact nonce");
        }
        finally
        {
            File.Delete(healthPath);
            File.Delete(healthPath + ".tmp");
        }
        foreach (var path in new[] { "../evil.exe", "/absolute.exe", "folder/../../escape", "C:/escape", "a\\b", "CON.txt", "a/..", "a/name.", "a/name ", "a:stream" })
            assert(Throws<InvalidDataException>(() => UpdatePolicy.ValidateRelativePath(path)), "Update rejects unsafe path " + path);

        var root = Path.Combine(Path.GetTempPath(), "SnailCommander-update-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var validZip = Path.Combine(root, "valid.zip");
            CreateZip(validZip, ("SnailCommander.exe", "new-app"), ("SnailCommander.Updater.exe", "new-updater"));
            var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(validZip)));
            UpdatePolicy.VerifyDigestAsync(validZip, digest).GetAwaiter().GetResult();
            assert(Throws<InvalidDataException>(() => UpdatePolicy.VerifyDigestAsync(validZip, null).GetAwaiter().GetResult()), "Missing digest fails closed");
            assert(Throws<InvalidDataException>(() => UpdatePolicy.VerifyDigestAsync(validZip, "sha256:" + new string('0', 64)).GetAwaiter().GetResult()), "Digest mismatch fails closed");
            var extract = Path.Combine(root, "extract");
            UpdatePolicy.ExtractPackage(validZip, extract);
            assert(File.ReadAllText(Path.Combine(extract, "SnailCommander.exe")) == "new-app", "Verified package extracts required executables");

            var badZip = Path.Combine(root, "bad.zip");
            CreateZip(badZip, ("SnailCommander.exe", "app"), ("SnailCommander.Updater.exe", "updater"), ("../outside", "evil"));
            var rejected = Path.Combine(root, "rejected");
            assert(Throws<InvalidDataException>(() => UpdatePolicy.ExtractPackage(badZip, rejected)) && !Directory.Exists(rejected), "ZIP traversal rejected before extracting any files");
            var duplicateZip = Path.Combine(root, "duplicate.zip");
            CreateZip(duplicateZip, ("SnailCommander.exe", "app"), ("SnailCommander.Updater.exe", "updater"), ("snailcommander.exe", "duplicate"));
            assert(Throws<InvalidDataException>(() => UpdatePolicy.ExtractPackage(duplicateZip, rejected)), "ZIP rejects case-insensitive Windows duplicate paths");

            var install = Path.Combine(root, "installed"); Directory.CreateDirectory(install);
            assert(Throws<InvalidDataException>(() => UpdateTransaction.Create(install, Path.Combine(install, "backup"))), "Backup cannot overlap installation directory");
            File.WriteAllText(Path.Combine(install, "SnailCommander.exe"), "old-app");
            File.WriteAllText(Path.Combine(install, "settings.json"), "keep-local-data");
            var transaction = UpdateTransaction.Create(install, Path.Combine(root, "backup"));
            assert(Throws<InvalidDataException>(() => transaction.Install(install)), "Staging cannot overlap installation directory");
            transaction.Install(extract);
            assert(transaction.Status == "awaiting-health" && File.ReadAllText(Path.Combine(install, "SnailCommander.exe")) == "new-app", "Install awaits startup acknowledgment");
            var recovered = UpdateTransaction.Load(transaction.BackupDirectory);
            recovered.Rollback();
            assert(File.ReadAllText(Path.Combine(install, "SnailCommander.exe")) == "old-app" && !File.Exists(Path.Combine(install, "SnailCommander.Updater.exe")), "Persisted rollback restores old files and removes newly added files");
            assert(File.ReadAllText(Path.Combine(install, "settings.json")) == "keep-local-data", "Update leaves unrelated data intact");

            var failed = UpdateTransaction.Create(install, Path.Combine(root, "failed-backup"));
            int copies = 0;
            assert(Throws<IOException>(() => failed.Install(extract, (source, target) =>
            {
                File.Copy(source, target, true);
                if (++copies == 2) throw new IOException("simulated mid-copy failure");
            })), "Mid-install failure is surfaced");
            failed.Rollback();
            assert(File.ReadAllText(Path.Combine(install, "SnailCommander.exe")) == "old-app" && !File.Exists(Path.Combine(install, "SnailCommander.Updater.exe")), "Mid-install rollback restores replaced and newly introduced files");

            var retention = Path.Combine(root, "retention");
            for (int i = 0; i < 5; i++) { var item = UpdateTransaction.Create(install, Path.Combine(retention, "backup-" + i)); item.Commit(); }
            UpdateTransaction.PruneHealthyBackups(retention, 2);
            assert(Directory.GetDirectories(retention).Length == 2 && Directory.Exists(Path.Combine(retention, "backup-4")), "Retention preserves newest known-good backups and bounds count");
        }
        finally
        {
            var resolved = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("SnailCommander-update-tests-", StringComparison.Ordinal))
                Directory.Delete(resolved, true);
        }
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; } catch (T) { return true; }
    }

    private static void CreateZip(string path, params (string Name, string Text)[] entries)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry.Name).Open()); writer.Write(entry.Text);
        }
    }
}

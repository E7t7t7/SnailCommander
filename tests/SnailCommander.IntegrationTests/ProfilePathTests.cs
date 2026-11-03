using System.Text.Json;
using SnailCommander;
using SnailCommander.Core.Updates;

internal static class ProfilePathTests
{
    public static int Run(string output)
    {
        output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(output));
        Directory.CreateDirectory(output);
        string profile = Path.Combine(output, "profile-" + Guid.NewGuid().ToString("N"));
        int assertions = 0;
        void Assert(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
        void Reject(string[] arguments)
        {
            try { AppPaths.Configure(arguments); }
            catch (ArgumentException) { assertions++; return; }
            throw new Exception("Invalid profile arguments were accepted.");
        }
        try
        {
            Assert(!AppPaths.UsesCustomRoot && AppPaths.DataDirectory == UpdateWorkerOptions.DefaultDataRoot,
                "Existing clients retain the normal LocalApplicationData directory.");
            Assert(CaptureStore.DirectoryPath == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnailCommander", "Captures"),
                "Existing clients retain the Pictures capture directory.");
            Reject(["--data-root"]);
            Reject(["--data-root", "--update-token"]);
            Reject(["--data-root", profile, "--data-root", profile]);
            Reject(["--data-root", Path.GetPathRoot(profile)!]);
            Reject(["--data-root", Path.Combine(AppContext.BaseDirectory, "profile")]);
            AppPaths.Configure(["--data-root", profile]);
            Assert(AppPaths.UsesCustomRoot && AppPaths.DataDirectory == profile, "Explicit profile is configured before stores initialize.");
            Assert(CaptureStore.DirectoryPath == Path.Combine(profile, "Captures"), "Captures remain inside the custom profile.");
            new LocalPreferences { LivePreviewEnabled = false, PreferredDevice = "profile-fixture" }.Save();
            Assert(File.Exists(Path.Combine(profile, "settings.json")), "Preferences are saved inside the custom profile.");
            var loaded = LocalPreferences.Load();
            Assert(!loaded.LivePreviewEnabled && loaded.PreferredDevice == "profile-fixture", "Profile preferences survive reload.");
            Assert(CaptureStore.LoadRecent().Count == 0 && Directory.Exists(Path.Combine(profile, "Captures")), "Empty profile history stays separate.");
            File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { assertions, failures = 0 }));
            Console.WriteLine($"{assertions} profile-path assertions passed; normal user data was not loaded or changed.");
            return 0;
        }
        finally
        {
            string target = Path.GetFullPath(profile);
            if (Directory.GetParent(target)?.FullName != output || !Path.GetFileName(target).StartsWith("profile-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe profile-test cleanup path.");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
    }
}

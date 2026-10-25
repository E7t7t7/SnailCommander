using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SnailCommander;

/// <summary>Stores a GitHub read-only token in the signed-in Windows user's Credential Manager.</summary>
internal static class GitHubTokenStore
{
    private const string Target = "SnailCommander/GitHubActionsReadOnly";
    private const uint Generic = 1;
    private const uint PersistLocalMachine = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credential);

    public static void Save(string token)
    {
        token = token.Trim();
        if (token.Length == 0) throw new ArgumentException("Enter a token before saving.");
        var bytes = Encoding.Unicode.GetBytes(token);
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            var cred = new Credential
            {
                Type = Generic,
                TargetName = Target,
                UserName = Environment.UserName,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = ptr,
                Persist = PersistLocalMachine
            };
            if (!CredWrite(ref cred, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not store the GitHub token.");
        }
        finally
        {
            Array.Clear(bytes, 0, bytes.Length);
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static string? Read()
    {
        if (!CredRead(Target, Generic, 0, out var ptr))
        {
            int err = Marshal.GetLastWin32Error();
            if (err == 1168) return null; // ERROR_NOT_FOUND
            throw new Win32Exception(err, "Windows could not read the GitHub token.");
        }
        try
        {
            var cred = Marshal.PtrToStructure<Credential>(ptr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0) return null;
            var bytes = new byte[checked((int)cred.CredentialBlobSize)];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            try { return Encoding.Unicode.GetString(bytes); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        finally { CredFree(ptr); }
    }

    public static void Clear()
    {
        if (!CredDelete(Target, Generic, 0) && Marshal.GetLastWin32Error() != 1168)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not remove the token.");
    }
}

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using CVC.RealtimeInterpreter.Core.Providers;

namespace CVC.RealtimeInterpreter.Core.Platforms.Windows;

/// <summary>API keys are scoped to the signed-in Windows user, independent of ProgramData settings.</summary>
public sealed class WindowsProviderCredentialStore : IProviderCredentialStore
{
    private const uint GenericCredential = 1;
    private const int NotFound = 1168;

    private static string Target(Guid id) => "CVC-RealtimeInterpreter/Providers/" + id.ToString("D");

    public Task<bool> HasKeyAsync(Guid providerId) => Task.Run(() =>
    {
        if (!CredRead(Target(providerId), GenericCredential, 0, out IntPtr pointer))
        {
            ThrowUnlessMissing();
            return false;
        }

        try
        {
            return Marshal.PtrToStructure<NativeCredential>(pointer).CredentialBlobSize > 0;
        }
        finally
        {
            CredFree(pointer);
        }
    });

    public Task<string?> ReadKeyAsync(Guid providerId) => Task.Run(() =>
    {
        if (!CredRead(Target(providerId), GenericCredential, 0, out IntPtr pointer))
        {
            ThrowUnlessMissing();
            return null;
        }

        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(pointer);
        }
    });

    public Task WriteKeyAsync(Guid providerId, string key) => Task.Run(() =>
    {
        int size = Encoding.Unicode.GetByteCount(key);
        if (size is 0 or > 2560)
        {
            throw new IOException("The API key must contain between 1 and 1280 characters.");
        }

        IntPtr blob = Marshal.StringToCoTaskMemUni(key);
        try
        {
            NativeCredential credential = new()
            {
                Type = GenericCredential,
                TargetName = Target(providerId),
                CredentialBlobSize = (uint)size,
                CredentialBlob = blob,
                Persist = 2,
                UserName = "CVC-RealtimeInterpreter"
            };
            if (!CredWrite(ref credential, 0))
            {
                throw StorageError();
            }
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    });

    public Task DeleteKeyAsync(Guid providerId) => Task.Run(() =>
    {
        if (!CredDelete(Target(providerId), GenericCredential, 0))
        {
            ThrowUnlessMissing();
        }
    });

    private static void ThrowUnlessMissing()
    {
        int error = Marshal.GetLastWin32Error();
        if (error != NotFound)
        {
            throw StorageError(error);
        }
    }

    private static IOException StorageError(int? error = null) => new("Windows Credential Manager: " +
        new Win32Exception(error ?? Marshal.GetLastWin32Error()).Message);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
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

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}

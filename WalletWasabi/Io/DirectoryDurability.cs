using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace WalletWasabi.Io;

internal static class DirectoryDurability
{
	public static void FlushContainingDirectory(string filePath)
	{
		if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsLinux()) { return; }
		var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
		// File-open flags differ between Linux architectures: 0x10000 is
		// O_DIRECTORY on x64 but O_DIRECT on ARM64. Let libc open the directory
		// with the platform's own flags, including close-on-exec.
		var stream = OpenDirectory(directory);
		if (stream == IntPtr.Zero) { throw new IOException("Could not open the containing directory for durability.", new Win32Exception(Marshal.GetLastPInvokeError())); }
		try
		{
			var descriptor = DirectoryDescriptor(stream);
			if (descriptor < 0) { throw new IOException("Could not access the containing directory for durability.", new Win32Exception(Marshal.GetLastPInvokeError())); }
			if (Fsync(descriptor) != 0) { throw new IOException("Could not persist the containing directory.", new Win32Exception(Marshal.GetLastPInvokeError())); }
		}
		finally { CloseDirectory(stream); }
	}

	[DllImport("libc", EntryPoint = "opendir", SetLastError = true)]
	private static extern IntPtr OpenDirectory([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
	[DllImport("libc", EntryPoint = "dirfd", SetLastError = true)]
	private static extern int DirectoryDescriptor(IntPtr stream);
	[DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
	private static extern int Fsync(int descriptor);
	[DllImport("libc", EntryPoint = "closedir")]
	private static extern int CloseDirectory(IntPtr stream);
}

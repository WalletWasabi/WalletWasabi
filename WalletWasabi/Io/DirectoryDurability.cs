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
		var descriptor = Open(directory, 0x10000); // O_RDONLY | O_DIRECTORY on Android/Linux.
		if (descriptor < 0) { throw new IOException("Could not open the containing directory for durability.", new Win32Exception(Marshal.GetLastPInvokeError())); }
		try
		{
			if (Fsync(descriptor) != 0) { throw new IOException("Could not persist the containing directory.", new Win32Exception(Marshal.GetLastPInvokeError())); }
		}
		finally { Close(descriptor); }
	}

	[DllImport("libc", EntryPoint = "open", SetLastError = true)]
	private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
	[DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
	private static extern int Fsync(int descriptor);
	[DllImport("libc", EntryPoint = "close")]
	private static extern int Close(int descriptor);
}

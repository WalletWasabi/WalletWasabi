using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace WalletWasabi.Fluent.Helpers;

public static class FileHelpers
{
	/// <seealso href="https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--1000-1299-"/>
	private const int ErrorNoAssociation = 1155; // ERROR_NO_ASSOCIATION

	private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".txt",
		".json",
		".log"
	};

	public static void OpenFileInTextEditor(string filePath)
	{
		string fullPath = Path.GetFullPath(filePath);

		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException($"The {Path.GetFileName(fullPath)} file is not found.", fullPath);
		}

		if (!AllowedExtensions.Contains(Path.GetExtension(fullPath)))
		{
			throw new NotSupportedException($"Opening '{Path.GetExtension(fullPath)}' files in a text editor is not supported.");
		}

		ProcessStartInfo startInfo;

		if (OperatingSystem.IsLinux())
		{
			// Opens the default handler for the file type, which is not guaranteed to be a text editor.
			startInfo = new ProcessStartInfo("xdg-open") { ArgumentList = { fullPath } };
		}
		else if (OperatingSystem.IsMacOS())
		{
			startInfo = new ProcessStartInfo("/usr/bin/open") { ArgumentList = { "-e", "--", fullPath } };
		}
		else if (OperatingSystem.IsWindows())
		{
			// Opens the file with the app associated with its type.
			startInfo = new ProcessStartInfo(fullPath) { UseShellExecute = true };
		}
		else
		{
			throw new PlatformNotSupportedException("Cannot open a text editor on this platform.");
		}

		try
		{
			using var process = Process.Start(startInfo);
		}
		catch (Win32Exception ex) when (OperatingSystem.IsWindows() && ex.NativeErrorCode == ErrorNoAssociation)
		{
			using var process = Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { fullPath } });
		}
		catch (Win32Exception ex)
		{
			throw new InvalidOperationException($"Failed to launch '{startInfo.FileName}'.", ex);
		}
	}
}

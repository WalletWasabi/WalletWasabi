using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class FileHelpers
{
	public static Task OpenFileInTextEditorAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"The {Path.GetFileName(filePath)} file is not found.");
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			using var process = Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { filePath }, UseShellExecute = false });
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			using var process = Process.Start(new ProcessStartInfo("open") { ArgumentList = { "-e", filePath }, UseShellExecute = false });
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			bool openWithNotepad = true; // If there is an exception with the registry read we use notepad.

			try
			{
				openWithNotepad = !EnvironmentHelpers.IsFileTypeAssociated(Path.GetExtension(filePath));
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}

			using var process = openWithNotepad
				? Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { filePath }, UseShellExecute = false })
				: Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
		}
		else
		{
			throw new PlatformNotSupportedException("Cannot open a text editor on this platform.");
		}

		return Task.CompletedTask;
	}
}

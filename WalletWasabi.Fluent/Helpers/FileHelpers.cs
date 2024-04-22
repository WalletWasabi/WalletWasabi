using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Helpers;

public static class FileHelpers
{
	public static async Task OpenFileInTextEditorAsync(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"The {Path.GetFileName(filePath)} file is not found.");
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			// If no associated application/json MimeType is found xdg-open opens return error
			// but it tries to open it anyway using the console editor (nano, vim, other..)
			await EnvironmentHelpers.ShellExecAsync($"which gedit &> /dev/null && gedit {filePath} || xdg-open {filePath}", waitForExit: false).ConfigureAwait(false);
		}
		else
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				bool openWithNotepad = true; // If there is an exception with the registry read we use notepad.

				try
				{
					openWithNotepad = !EnvironmentHelpers.IsFileTypeAssociated("json");
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
				}

				if (openWithNotepad)
				{
					// Open file using Notepad.
					using var notepadProcess = Process.Start(new ProcessStartInfo
					{
						FileName = "notepad.exe",
						Arguments = filePath,
						CreateNoWindow = true,
						UseShellExecute = false
					});
					return; // Opened with notepad, return.
				}
			}

			// Open file with the default editor.
			using var defaultEditorProcess = Process.Start(new ProcessStartInfo
			{
				FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? filePath : "open",
				Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"-e {filePath}" : "",
				CreateNoWindow = true,
				UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			});
		}
	}
}

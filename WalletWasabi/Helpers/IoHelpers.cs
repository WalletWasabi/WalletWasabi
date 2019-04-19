using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace System.IO
{
	public static class IoHelpers
	{
		private const string OldExtension = ".old";
		private const string NewExtension = ".new";

		// http://stackoverflow.com/a/14933880/2061103
		public static async Task DeleteRecursivelyWithMagicDustAsync(string destinationDir)
		{
			const int magicDust = 10;
			for (var gnomes = 1; gnomes <= magicDust; gnomes++)
			{
				try
				{
					Directory.Delete(destinationDir, true);
				}
				catch (DirectoryNotFoundException)
				{
					return;  // good!
				}
				catch (IOException)
				{
					if (gnomes == magicDust)
					{
						throw;
					}
					// System.IO.IOException: The directory is not empty
					Logger.LogDebug($"Gnomes prevent deletion of {destinationDir}! Applying magic dust, attempt #{gnomes}.", nameof(IoHelpers));

					// see http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true for more magic
					await Task.Delay(100);
					continue;
				}
				catch (UnauthorizedAccessException)
				{
					if (gnomes == magicDust)
					{
						throw;
					}
					// Wait, maybe another software make us authorized a little later
					Logger.LogDebug($"Gnomes prevent deletion of {destinationDir}! Applying magic dust, attempt #{gnomes}.", nameof(IoHelpers));

					// see http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true for more magic
					await Task.Delay(100);
					continue;
				}
				return;
			}
			// depending on your use case, consider throwing an exception here
		}

		// https://stackoverflow.com/a/7957634/2061103
		private static void SafeMove(string newPath, string path)
		{
			var oldPath = path + OldExtension;
			if (File.Exists(oldPath))
			{
				File.Delete(oldPath);
			}

			if (File.Exists(path))
			{
				File.Move(path, oldPath);
			}

			File.Move(newPath, path);

			File.Delete(oldPath);
		}

		public static void SafeWriteAllText(string path, string content)
		{
			var newPath = path + NewExtension;
			var mutexName = $"Global\\4AA0E5A2-A94F-4B92-B962-F2BBC7A68323-{Path.GetFileNameWithoutExtension(path)}";
			using (var mutex = new Mutex(false, mutexName))
			{
				var mutexAcquired = false;
				try
				{
					// acquire the mutex (or timeout after 60 seconds)
					// will return false if it timed out
					mutexAcquired = mutex.WaitOne(60000);
				}
				catch (AbandonedMutexException)
				{
					// abandoned mutexes are still acquired, we just need
					// to handle the exception and treat it as acquisition
					mutexAcquired = true;
				}

				if (mutexAcquired == false)
				{
					throw new IOException("Couldn't acquire Mutex on the file.");
				}

				try
				{
					File.WriteAllText(newPath, content);
					SafeMove(newPath, path);
				}
				finally
				{
					mutex.ReleaseMutex();
				}
			}
		}

		public static void SafeWriteAllText(string path, string content, Encoding encoding)
		{
			var newPath = path + NewExtension;
			var mutexName = $"Global\\4AA0E5A2-A94F-4B92-B962-F2BBC7A68323-{Path.GetFileNameWithoutExtension(path)}";
			using (var mutex = new Mutex(false, mutexName))
			{
				var mutexAcquired = false;
				try
				{
					// acquire the mutex (or timeout after 60 seconds)
					// will return false if it timed out
					mutexAcquired = mutex.WaitOne(60000);
				}
				catch (AbandonedMutexException)
				{
					// abandoned mutexes are still acquired, we just need
					// to handle the exception and treat it as acquisition
					mutexAcquired = true;
				}

				if (mutexAcquired == false)
				{
					throw new IOException("Couldn't acquire Mutex on the file.");
				}

				try
				{
					File.WriteAllText(newPath, content, encoding);
					SafeMove(newPath, path);
				}
				finally
				{
					mutex.ReleaseMutex();
				}
			}
		}

		public static void SafeWriteAllLines(string path, IEnumerable<string> content)
		{
			var newPath = path + NewExtension;
			var mutexName = $"Global\\4AA0E5A2-A94F-4B92-B962-F2BBC7A68323-{Path.GetFileNameWithoutExtension(path)}";
			using (var mutex = new Mutex(false, mutexName))
			{
				var mutexAcquired = false;
				try
				{
					// acquire the mutex (or timeout after 60 seconds)
					// will return false if it timed out
					mutexAcquired = mutex.WaitOne(60000);
				}
				catch (AbandonedMutexException)
				{
					// abandoned mutexes are still acquired, we just need
					// to handle the exception and treat it as acquisition
					mutexAcquired = true;
				}

				if (mutexAcquired == false)
				{
					throw new IOException("Couldn't acquire Mutex on the file.");
				}

				try
				{
					File.WriteAllLines(newPath, content);
					SafeMove(newPath, path);
				}
				finally
				{
					mutex.ReleaseMutex();
				}
			}
		}

		public static async Task BetterExtractZipToDirectoryAsync(string src, string dest)
		{
			try
			{
				ZipFile.ExtractToDirectory(src, dest);
			}
			catch (UnauthorizedAccessException)
			{
				await Task.Delay(100);
				ZipFile.ExtractToDirectory(src, dest);
			}
		}

		// https://stackoverflow.com/a/7957634/2061103
		public static bool TryGetSafestFileVersion(string path, out string safestFilePath)
		{
			var newPath = path + NewExtension;
			var oldPath = path + OldExtension;

			// If foo.data and foo.data.new exist, load foo.data; foo.data.new may be broken (e.g. power off during write).
			if (File.Exists(path) && File.Exists(newPath))
			{
				safestFilePath = path;
				return true;
			}

			// If foo.data.old and foo.data.new exist, both should be valid, but something died very shortly afterwards - you may want to load the foo.data.old version anyway.
			if (File.Exists(oldPath) && File.Exists(newPath))
			{
				safestFilePath = oldPath;
				return true;
			}

			// If foo.data and foo.data.old exist, then foo.data should be fine, but again something went wrong, or possibly the file couldn't be deleted.
			if (File.Exists(oldPath) && File.Exists(path))
			{
				safestFilePath = path;
				return true;
			}

			if (File.Exists(path))
			{
				safestFilePath = path;
				return true;
			}

			safestFilePath = null;
			return false;
		}

		public static void EnsureContainingDirectoryExists(string fileNameOrPath)
		{
			string fullPath = Path.GetFullPath(fileNameOrPath); // No matter if relative or absolute path is given to this.
			string dir = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrEmpty(dir)) // root
			{
				Directory.CreateDirectory(dir); // It does not fail if it exists.
			}
		}

		public static byte[] GetHashFile(string filePath)
		{
			var bytes = File.ReadAllBytes(filePath);
			using (var sha = new SHA256Managed())
			{
				return sha.ComputeHash(bytes, 0, bytes.Length);
			}
		}

		public static bool CheckExpectedHash(string filePath, string sourceFolderPath)
		{
			var fileHash = GetHashFile(filePath);
			try
			{
				var digests = File.ReadAllLines(Path.Combine(sourceFolderPath, "digests.txt"));
				foreach (var digest in digests)
				{
					var expectedHash = ByteHelpers.FromHex(digest);
					if (ByteHelpers.CompareFastUnsafe(fileHash, expectedHash))
					{
						return true;
					}
				}
				return false;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static void OpenFolderInFileExplorer(string dirPath)
		{
			if (Directory.Exists(dirPath))
			{
				using (Process process = Process.Start(new ProcessStartInfo
				{
					FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "explorer.exe" : (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open"),
					Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"\"{dirPath}\"" : dirPath,
					CreateNoWindow = true
				})) { }
			}
		}

		public static void OpenFileInTextEditor(string filePath)
		{
			if (File.Exists(filePath))
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					// If no associated application/json MimeType is found xdg-open opens retrun error
					// but it tries to open it anyway using the console editor (nano, vim, other..)
					EnvironmentHelpers.ShellExec($"gedit {filePath} || xdg-open {filePath}", waitForExit: false);
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
							Logger.LogError(ex, nameof(IoHelpers));
						}

						if (openWithNotepad)
						{
							// Open file using Notepad.
							using (Process process = Process.Start(new ProcessStartInfo
							{
								FileName = "notepad.exe",
								Arguments = filePath,
								CreateNoWindow = true,
								UseShellExecute = false
							})) { }

							return; // Opened with notepad, return.
						}
					}

					// Open file wtih the default editor.
					using (Process process = Process.Start(new ProcessStartInfo
					{
						FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? filePath : "open",
						Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"-e {filePath}" : "",
						CreateNoWindow = true,
						UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
					})) { }
				}
			}
		}

		public static void OpenBrowser(string url)
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// If no associated application/json MimeType is found xdg-open opens retrun error
				// but it tries to open it anyway using the console editor (nano, vim, other..)
				EnvironmentHelpers.ShellExec($"xdg-open {url}", waitForExit: false);
			}
			else
			{
				using (Process process = Process.Start(new ProcessStartInfo
				{
					FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? url : "open",
					Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"-e {url}" : "",
					CreateNoWindow = true,
					UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				})) { }
			}
		}

		public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
		{
			foreach (DirectoryInfo dir in source.GetDirectories())
			{
				CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
			}

			foreach (FileInfo file in source.GetFiles())
			{
				file.CopyTo(Path.Combine(target.FullName, file.Name));
			}
		}
	}
}

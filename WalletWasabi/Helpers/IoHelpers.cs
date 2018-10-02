using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace System.IO
{
	public static class IoHelpers
	{
		private const string OldExtension = ".old";
		private const string NewExtension = ".new";
		private const string LockExtension = ".lock";

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
						throw;
					// System.IO.IOException: The directory is not empty
					Logger.LogDebug($"Gnomes prevent deletion of {destinationDir}! Applying magic dust, attempt #{gnomes}.", nameof(IoHelpers));

					// see http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true for more magic
					await Task.Delay(100);
					continue;
				}
				catch (UnauthorizedAccessException)
				{
					if (gnomes == magicDust)
						throw;
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

		public static async Task SafeWriteAllTextAsync(string path, string content)
		{
			string newPath = path + NewExtension;
			string lockPath = CreateLockOrDelayWhileExists(path);
			try
			{
				await File.WriteAllTextAsync(newPath, content);
				SafeMove(newPath, path);
			}
			finally
			{
				DeleteLock(lockPath);
			}
		}

		public static void SafeWriteAllText(string path, string content, Encoding encoding)
		{
			Logger.LogInfo("SafeWriteAllText");
			string newPath = path + NewExtension;
			Logger.LogInfo("string newPath = path + NewExtension;");
			string lockPath = CreateLockOrDelayWhileExists(path);
			Logger.LogInfo("string lockPath = CreateLockOrDelayWhileExists(path);");
			try
			{
				Logger.LogInfo("try");
				File.WriteAllText(newPath, content, encoding);
				Logger.LogInfo("File.WriteAllText(newPath, content, encoding);");
				SafeMove(newPath, path);
				Logger.LogInfo("SafeMove(newPath, path);");
			}
			finally
			{
				Logger.LogInfo("finally");
				DeleteLock(lockPath);
				Logger.LogInfo("RemoveLockPath(lockPath);");
			}
		}

		public static async Task SafeWriteAllTextAsync(string path, string content, Encoding encoding)
		{
			Logger.LogInfo("SafeWriteAllTextAsync");
			string newPath = path + NewExtension;
			Logger.LogInfo("string newPath = path + NewExtension;");
			string lockPath = CreateLockOrDelayWhileExists(path);
			Logger.LogInfo("string lockPath = CreateLockOrDelayWhileExists(path);");
			try
			{
				Logger.LogInfo("try");
				await File.WriteAllTextAsync(newPath, content, encoding);
				Logger.LogInfo("await File.WriteAllTextAsync(newPath, content, encoding);");
				SafeMove(newPath, path);
				Logger.LogInfo("SafeMove(newPath, path);");
			}
			finally
			{
				Logger.LogInfo("finally");
				DeleteLock(lockPath);
				Logger.LogInfo("RemoveLockPath(lockPath);");
			}
		}

		public static void SafeWriteAllLines(string path, IEnumerable<string> content)
		{
			Logger.LogInfo("SafeWriteAllLines");
			string newPath = path + NewExtension;
			Logger.LogInfo("string newPath = path + NewExtension;");
			string lockPath = CreateLockOrDelayWhileExists(path);
			Logger.LogInfo("string lockPath = CreateLockOrDelayWhileExists(path);");
			try
			{
				Logger.LogInfo("try");
				File.WriteAllLines(newPath, content);
				Logger.LogInfo("File.WriteAllLines(newPath, content);");
				SafeMove(newPath, path);
				Logger.LogInfo("SafeMove(newPath, path);");
			}
			finally
			{
				Logger.LogInfo("finally");
				DeleteLock(lockPath);
				Logger.LogInfo("RemoveLockPath(lockPath);");
			}
		}

		public static async Task SafeWriteAllLinesAsync(string path, IEnumerable<string> content)
		{
			Logger.LogInfo("SafeWriteAllLinesAsync");
			string newPath = path + NewExtension;
			Logger.LogInfo("string newPath = path + NewExtension;");
			string lockPath = CreateLockOrDelayWhileExists(path);
			Logger.LogInfo("string lockPath = CreateLockOrDelayWhileExists(path);");
			try
			{
				Logger.LogInfo("try");
				await File.WriteAllLinesAsync(newPath, content);
				Logger.LogInfo("await File.WriteAllLinesAsync(newPath, content);");
				SafeMove(newPath, path);
				Logger.LogInfo("SafeMove(newPath, path);");
			}
			finally
			{
				Logger.LogInfo("finally");
				DeleteLock(lockPath);
				Logger.LogInfo("RemoveLockPath(lockPath);");
			}
		}

		public static async Task SafeWriteAllBytesAsync(string path, byte[] content)
		{
			string newPath = path + NewExtension;
			string lockPath = CreateLockOrDelayWhileExists(path);
			try
			{
				await File.WriteAllBytesAsync(newPath, content);
				SafeMove(newPath, path);
			}
			finally
			{
				DeleteLock(lockPath);
			}
		}

		/// <param name="delayTimesBeforeForceIn">Times * 100ms delay before it forces itself into the lock.</param>
		/// <returns>lock file path</returns>
		private static string CreateLockOrDelayWhileExists(string path, int delayTimesBeforeForceIn = 70)
		{
			// This function is blocking, because .NET Core async brainfart.
			Logger.LogInfo("CreateLockOrDelayWhileExists");
			string lockPath = path + LockExtension;
			Logger.LogInfo("string lockPath = path + LockExtension;");
			var count = 0;
			Logger.LogInfo("var count = 0;");
			while (File.Exists(lockPath))
			{
				Logger.LogInfo("while (File.Exists(lockPath))");
				Thread.Sleep(100);
				Logger.LogInfo("Thread.Sleep(100);");
				if (count > delayTimesBeforeForceIn)
				{
					Logger.LogInfo("if (count > delayTimesBeforeForceIn)");
					return lockPath;
				}
				count++;
				Logger.LogInfo("count++;");
			}
			File.Create(lockPath);
			Logger.LogInfo("File.Create(lockPath);");
			return lockPath;
		}

		private static void DeleteLock(string lockPath)
		{
			Logger.LogInfo("DeleteLock");
			try
			{
				Logger.LogInfo("try");
				// .NET Core brainfart
				// https://stackoverflow.com/a/18278033/2061103
				GC.Collect();
				GC.WaitForPendingFinalizers();
				File.Delete(lockPath);
				Logger.LogInfo("File.Delete(lockPath);");
			}
			catch (Exception ex)
			{
				Logger.LogInfo("catch (Exception ex)");
				Logger.LogDebug(ex, nameof(IoHelpers));
				Logger.LogInfo("Logger.LogDebug(ex, nameof(IoHelpers));");
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

		public static void OpenFolderInFileExplorer(string dirPath)
		{
			if (Directory.Exists(dirPath))
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{dirPath}\"" });
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = dirPath, CreateNoWindow = true });
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start(new ProcessStartInfo { FileName = "open", Arguments = dirPath, CreateNoWindow = true });
				}
			}
		}
	}
}

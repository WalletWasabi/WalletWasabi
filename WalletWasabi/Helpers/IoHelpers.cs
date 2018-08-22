using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace System.IO
{
	public static class IoHelpers
	{
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
			var oldPath = path + ".old";
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
			var newPath = path + ".new";
			File.WriteAllText(newPath, content, Encoding.UTF8);
			SafeMove(newPath, path);
		}

		public static async Task SafeWriteAllTextAsync(string path, string content)
		{
			var newPath = path + ".new";
			await File.WriteAllTextAsync(newPath, content, Encoding.UTF8);
			SafeMove(newPath, path);
		}

		public static void WriteAllLines(string path, IEnumerable<string> content)
		{
			var newPath = path + ".new";
			File.WriteAllLines(newPath, content);
			SafeMove(newPath, path);
		}

		public static async Task SafeWriteAllLinesAsync(string path, IEnumerable<string> content)
		{
			var newPath = path + ".new";
			await File.WriteAllLinesAsync(newPath, content);
			SafeMove(newPath, path);
		}

		public static async Task SafeWriteAllBytesAsync(string path, byte[] content)
		{
			var newPath = path + ".new";
			await File.WriteAllBytesAsync(newPath, content);
			SafeMove(newPath, path);
		}

		public static bool TryGetSafestFileVersion(string path, out string safestFilePath)
		{
			var newPath = path + ".new";
			var oldPath = path + ".old";

			if (File.Exists(path) && File.Exists(newPath))
			{
				safestFilePath = path;
				return true;
			}
			if (File.Exists(oldPath) && File.Exists(newPath))
			{
				safestFilePath = oldPath;
				return true;
			}
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
	}
}

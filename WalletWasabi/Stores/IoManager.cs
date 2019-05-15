using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using Nito.AsyncEx;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// Safely manager file operations.
	/// </summary>
	public class IoManager
	{
		public string OriginalFilePath { get; }
		public string OldFilePath { get; }
		public string NewFilePath { get; }
		public string DigestFilePath { get; }

		public string FileName { get; }
		public string FileNameWithoutExtension { get; }
		public AsyncMutex Mutex { get; }

		private const string OldExtension = ".old";
		private const string NewExtension = ".new";
		private const string DigestExtension = ".dig";

		public IoManager(string filePath)
		{
			OriginalFilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
			OldFilePath = $"{OriginalFilePath}{OldExtension}";
			NewFilePath = $"{OriginalFilePath}{NewExtension}";
			DigestFilePath = $"{OriginalFilePath}{DigestExtension}";

			FileName = Path.GetFileName(OriginalFilePath);
			FileNameWithoutExtension = Path.GetFileNameWithoutExtension(OriginalFilePath);

			// https://docs.microsoft.com/en-us/dotnet/api/system.threading.mutex?view=netframework-4.8
			// On a server that is running Terminal Services, a named system mutex can have two levels of visibility.
			// If its name begins with the prefix "Global\", the mutex is visible in all terminal server sessions.
			// If its name begins with the prefix "Local\", the mutex is visible only in the terminal server session where it was created.
			// In that case, a separate mutex with the same name can exist in each of the other terminal server sessions on the server.
			// If you do not specify a prefix when you create a named mutex, it takes the prefix "Local\".
			// Within a terminal server session, two mutexes whose names differ only by their prefixes are separate mutexes,
			// and both are visible to all processes in the terminal server session.
			// That is, the prefix names "Global\" and "Local\" describe the scope of the mutex name relative to terminal server sessions, not relative to processes.
			Mutex = new AsyncMutex(FileNameWithoutExtension);
		}

		#region IoOperations

		public void DeleteMe()
		{
			if (File.Exists(OriginalFilePath))
			{
				File.Delete(OriginalFilePath);
			}

			if (File.Exists(NewFilePath))
			{
				File.Delete(NewFilePath);
			}

			if (File.Exists(OldFilePath))
			{
				File.Delete(OldFilePath);
			}

			if (File.Exists(DigestFilePath))
			{
				File.Delete(DigestFilePath);
			}
		}

		public bool TryReplaceMeWith(string sourcePath)
		{
			if (!File.Exists(sourcePath))
			{
				return false;
			}

			SafeMoveToOriginal(sourcePath);

			return true;
		}

		// https://stackoverflow.com/a/7957634/2061103
		private void SafeMoveNewToOriginal()
		{
			SafeMoveToOriginal(NewFilePath);
		}

		/// <summary>
		/// Source must exist.
		/// </summary>
		private void SafeMoveToOriginal(string source)
		{
			if (File.Exists(OriginalFilePath))
			{
				if (File.Exists(OldFilePath))
				{
					File.Delete(OldFilePath);
				}

				File.Move(OriginalFilePath, OldFilePath);
			}

			File.Move(source, OriginalFilePath);

			if (File.Exists(OldFilePath))
			{
				File.Delete(OldFilePath);
			}
		}

		/// <summary>
		/// https://stackoverflow.com/questions/7957544/how-to-ensure-that-data-doesnt-get-corrupted-when-saving-to-file/7957634#7957634
		/// </summary>
		private bool TryGetSafestFileVersion(out string safestFilePath)
		{
			// If foo.data and foo.data.new exist, load foo.data; foo.data.new may be broken (e.g. power off during write).
			bool newExists = File.Exists(NewFilePath);
			bool originalExists = File.Exists(OriginalFilePath);
			if (newExists && originalExists)
			{
				safestFilePath = OriginalFilePath;
				return true;
			}

			// If foo.data.old and foo.data.new exist, both should be valid, but something died very shortly afterwards
			// - you may want to load the foo.data.old version anyway
			bool oldExists = File.Exists(OldFilePath);
			if (newExists && oldExists)
			{
				safestFilePath = OldFilePath;
				return true;
			}

			// If foo.data and foo.data.old exist, then foo.data should be fine, but again something went wrong, or possibly the file couldn't be deleted.
			// if (File.Exists(originalPath) && File.Exists(oldPath))
			if (originalExists)
			{
				safestFilePath = OriginalFilePath;
				return true;
			}

			safestFilePath = null;
			return false;
		}

		public bool Exists()
		{
			return TryGetSafestFileVersion(out _);
		}

		public async Task WriteAllLinesAsync(IEnumerable<string> contents, CancellationToken cancellationToken = default)
		{
			byte[] hash = null;
			try
			{
				IEnumerable<byte[]> arrays = contents.Select(x => Encoding.ASCII.GetBytes(x));
				byte[] bytes = ByteHelpers.Combine(arrays);
				hash = IoHelpers.GetHash(bytes);
				if (File.Exists(DigestFilePath))
				{
					var digest = await File.ReadAllBytesAsync(DigestFilePath, cancellationToken);
					if (ByteHelpers.CompareFastUnsafe(hash, digest))
					{
						return;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IoManager>("Failed to read digest.");
				Logger.LogInfo<IoManager>(ex);
			}

			IoHelpers.EnsureContainingDirectoryExists(NewFilePath);

			await File.WriteAllLinesAsync(NewFilePath, contents, cancellationToken);
			SafeMoveNewToOriginal();

			try
			{
				IoHelpers.EnsureContainingDirectoryExists(DigestFilePath);

				await File.WriteAllBytesAsync(DigestFilePath, hash);
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IoManager>("Failed to create digest.");
				Logger.LogInfo<IoManager>(ex);
			}
		}

		// Can't compute hash for append.
		//public async Task AppendAllLinesAsync(IEnumerable<string> contents, CancellationToken cancellationToken = default)
		//{
		//	// Make sure the NewFilePath exists.
		//	if (TryGetSafestFileVersion(out string safestFilePath) && safestFilePath != NewFilePath)
		//	{
		//		File.Copy(safestFilePath, NewFilePath, overwrite: true);
		//	}

		//	await File.AppendAllLinesAsync(NewFilePath, contents, cancellationToken);
		//	SafeMoveNewToOriginal();
		//}

		public async Task<string[]> ReadAllLinesAsync(CancellationToken cancellationToken = default)
		{
			var filePath = OriginalFilePath;
			if (TryGetSafestFileVersion(out string safestFilePath))
			{
				filePath = safestFilePath;
			}
			return await File.ReadAllLinesAsync(filePath, cancellationToken);
		}

		public StreamReader OpenText()
		{
			var filePath = OriginalFilePath;
			if (TryGetSafestFileVersion(out string safestFilePath))
			{
				filePath = safestFilePath;
			}

			return File.OpenText(filePath);
		}

		#endregion IoOperations
	}
}

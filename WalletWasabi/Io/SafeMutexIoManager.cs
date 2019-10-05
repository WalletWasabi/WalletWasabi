using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Io
{
	public class SafeMutexIoManager : MutexIoManager
	{
		public string OldFilePath { get; }
		public string NewFilePath { get; }

		private const string OldExtension = ".old";
		private const string NewExtension = ".new";

		public SafeMutexIoManager(string filePath) : base(filePath)
		{
			OldFilePath = $"{FilePath}{OldExtension}";

			NewFilePath = $"{FilePath}{NewExtension}";
		}

		#region IoOperations

		public new void DeleteMe()
		{
			base.DeleteMe();

			if (File.Exists(NewFilePath))
			{
				File.Delete(NewFilePath);
			}

			if (File.Exists(OldFilePath))
			{
				File.Delete(OldFilePath);
			}
		}

		public new bool TryReplaceMeWith(string sourcePath)
		{
			if (File.Exists(sourcePath))
			{
				SafeMoveToOriginal(sourcePath);

				return true;
			}
			else
			{
				return false;
			}
		}

		// https://stackoverflow.com/a/7957634/2061103
		protected void SafeMoveNewToOriginal()
		{
			SafeMoveToOriginal(NewFilePath);
		}

		/// <summary>
		/// Source must exist.
		/// </summary>
		private void SafeMoveToOriginal(string source)
		{
			if (File.Exists(FilePath))
			{
				if (File.Exists(OldFilePath))
				{
					File.Delete(OldFilePath);
				}

				File.Move(FilePath, OldFilePath);
			}

			File.Move(source, FilePath);

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
			bool originalExists = File.Exists(FilePath);
			if (newExists && originalExists)
			{
				safestFilePath = FilePath;
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

			// If foo.data and foo.data.old exist, then foo.data should be fine, but again something went wrong, or possibly the file could not be deleted.
			// if (File.Exists(originalPath) && File.Exists(oldPath))
			if (originalExists)
			{
				safestFilePath = FilePath;
				return true;
			}

			safestFilePath = null;
			return false;
		}

		public new bool Exists()
		{
			return TryGetSafestFileVersion(out _);
		}

		public new async Task WriteAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
		{
			if (lines is null || !lines.Any())
			{
				return;
			}

			IoHelpers.EnsureContainingDirectoryExists(NewFilePath);

			await File.WriteAllLinesAsync(NewFilePath, lines, cancellationToken).ConfigureAwait(false);
			SafeMoveNewToOriginal();
		}

		public new async Task<string[]> ReadAllLinesAsync(CancellationToken cancellationToken = default)
		{
			var filePath = FilePath;
			if (TryGetSafestFileVersion(out string safestFilePath))
			{
				filePath = safestFilePath;
			}
			return await ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Open text file and read a specified amount of data. This method is useful when you want async/await read/write
		/// but in a performant way.
		/// </summary>
		/// <returns>The StreamReader where you can use ReadLineAsync() for example.</returns>
		/// <param name="bufferSize">Size of the bytes to handle sync way. The default is 1Mb.</param>
		public new StreamReader OpenText(int bufferSize = Constants.BigFileReadWriteBufferSize)
		{
			var filePath = FilePath;
			if (TryGetSafestFileVersion(out string safestFilePath))
			{
				filePath = safestFilePath;
			}

			return OpenText(filePath, bufferSize);
		}

		#endregion IoOperations
	}
}

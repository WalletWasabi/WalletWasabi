using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// Safely manager file operations.
	/// </summary>
	public class IoManager : FileAsyncMutexProvider
	{
		public string OldFilePath { get; }
		public string NewFilePath { get; }
		public string DigestFilePath { get; }

		/// <summary>
		/// Use the random index of the line to create digest faster. -1 is special value, it means the last character. If null then hash whole file.
		/// </summary>
		private int? DigestRandomIndex { get; }

		private const string OldExtension = ".old";
		private const string NewExtension = ".new";
		private const string DigestExtension = ".dig";

		/// <param name="digestRandomIndex">Use the random index of the line to create digest faster. -1 is special value, it means the last character. If null then hash whole file.</param>
		public IoManager(string filePath, int? digestRandomIndex = null) : base(filePath)
		{
			DigestRandomIndex = digestRandomIndex;
			OldFilePath = $"{FilePath}{OldExtension}";
			NewFilePath = $"{FilePath}{NewExtension}";
			DigestFilePath = $"{FilePath}{DigestExtension}";
		}

		#region IoOperations

		public void DeleteMe()
		{
			if (File.Exists(FilePath))
			{
				File.Delete(FilePath);
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

			// If foo.data and foo.data.old exist, then foo.data should be fine, but again something went wrong, or possibly the file couldn't be deleted.
			// if (File.Exists(originalPath) && File.Exists(oldPath))
			if (originalExists)
			{
				safestFilePath = FilePath;
				return true;
			}

			safestFilePath = null;
			return false;
		}

		public bool Exists()
		{
			return TryGetSafestFileVersion(out _);
		}

		public async Task WriteAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default, bool dismissNullOrEmptyContent = true)
		{
			if (dismissNullOrEmptyContent)
			{
				if (lines is null || !lines.Any())
				{
					return;
				}
			}

			var byteArrayBuilder = new ByteArrayBuilder();
			foreach (var line in lines)
			{
				ContinueBuildHash(byteArrayBuilder, line);
			}

			var res = await WorkWithHashAsync(byteArrayBuilder, cancellationToken);
			if (res.same)
			{
				return;
			}

			IoHelpers.EnsureContainingDirectoryExists(NewFilePath);

			await File.WriteAllLinesAsync(NewFilePath, lines, cancellationToken);
			SafeMoveNewToOriginal();
			await WriteOutHashAsync(res.hash);
		}

		private async Task<(bool same, byte[] hash)> WorkWithHashAsync(ByteArrayBuilder byteArrayBuilder, CancellationToken cancellationToken)
		{
			byte[] hash = null;
			try
			{
				var bytes = byteArrayBuilder.ToArray();
				hash = HashHelpers.GenerateSha256Hash(bytes);
				if (File.Exists(DigestFilePath))
				{
					var digest = await File.ReadAllBytesAsync(DigestFilePath, cancellationToken);
					if (ByteHelpers.CompareFastUnsafe(hash, digest))
					{
						if (File.Exists(NewFilePath))
						{
							File.Delete(NewFilePath);
						}
						return (true, hash);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning<IoManager>("Failed to read digest.");
				Logger.LogInfo<IoManager>(ex);
			}

			return (false, hash);
		}

		private async Task WriteOutHashAsync(byte[] hash)
		{
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

		public async Task AppendAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
		{
			if (lines is null || !lines.Any())
			{
				return;
			}

			IoHelpers.EnsureContainingDirectoryExists(NewFilePath);
			if (File.Exists(NewFilePath))
			{
				File.Delete(NewFilePath);
			}

			var byteArrayBuilder = new ByteArrayBuilder();

			var linesArray = lines.ToArray();
			var linesIndex = 0;

			using (var sr = OpenText())
			using (var fs = File.OpenWrite(NewFilePath))
			using (var sw = new StreamWriter(fs, Encoding.ASCII, Constants.BigFileReadWriteBufferSize))
			{
				// 1. First copy.
				if (!sr.EndOfStream)
				{
					var lineTask = sr.ReadLineAsync();
					Task wTask = Task.CompletedTask;
					string line = null;
					while (lineTask != null)
					{
						if (line is null)
						{
							line = await lineTask;
						}

						lineTask = sr.EndOfStream ? null : sr.ReadLineAsync();

						if (linesArray[linesIndex] == line) // If the line is a line we want to write, then we know that someone else have worked into the file.
						{
							linesIndex++;
							continue;
						}

						await wTask;
						wTask = sw.WriteLineAsync(line);

						ContinueBuildHash(byteArrayBuilder, line);

						cancellationToken.ThrowIfCancellationRequested();

						line = null;
					}
					await wTask;
				}
				await sw.FlushAsync();

				// 2. Then append.
				foreach (var line in linesArray)
				{
					await sw.WriteLineAsync(line);

					ContinueBuildHash(byteArrayBuilder, line);

					cancellationToken.ThrowIfCancellationRequested();
				}

				await sw.FlushAsync();
			}

			var res = await WorkWithHashAsync(byteArrayBuilder, cancellationToken);
			if (res.same)
			{
				return;
			}

			SafeMoveNewToOriginal();
			await WriteOutHashAsync(res.hash);
		}

		public async Task<string[]> ReadAllLinesAsync(CancellationToken cancellationToken = default)
		{
			var filePath = FilePath;
			if (TryGetSafestFileVersion(out string safestFilePath))
			{
				filePath = safestFilePath;
			}
			return await File.ReadAllLinesAsync(filePath, cancellationToken);
		}

		/// <summary>
		/// Open text file and read a specified amount of data. This method is useful when you want async/await read/write
		/// but in a performant way.
		/// </summary>
		/// <returns>The StreamReader where you can use ReadLineAsync() for example.</returns>
		/// <param name="bufferSize">Size of the bytes to handle sync way. The default is 1Mb.</param>
		public StreamReader OpenText(int bufferSize = Constants.BigFileReadWriteBufferSize)
		{
			var filePath = FilePath;
			if (TryGetSafestFileVersion(out string safestFilePath))
			{
				filePath = safestFilePath;
			}

			var fs = File.OpenRead(filePath);
			return new StreamReader(fs, Encoding.ASCII, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize, leaveOpen: false);
		}

		#endregion IoOperations

		#region HashBuilding

		private void ContinueBuildHash(ByteArrayBuilder byteArrayBuilder, string line)
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				byteArrayBuilder.Append(0);
			}
			else
			{
				if (DigestRandomIndex.HasValue)
				{
					int index;
					if (DigestRandomIndex == -1 || DigestRandomIndex >= line.Length) // Last char.
					{
						index = line.Length - 1;
					}
					else
					{
						index = DigestRandomIndex.Value;
					}

					var c = line[index];
					var b = (byte)c;
					byteArrayBuilder.Append(b);
				}
				else
				{
					var b = Encoding.ASCII.GetBytes(line);
					byteArrayBuilder.Append(b);
				}
			}
		}

		#endregion HashBuilding
	}
}

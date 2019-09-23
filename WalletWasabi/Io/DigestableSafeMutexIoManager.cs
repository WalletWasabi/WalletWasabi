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

namespace WalletWasabi.Io
{
	/// <summary>
	/// Safely manager file operations.
	/// </summary>
	public class DigestableSafeMutexIoManager : SafeMutexIoManager
	{
		public string DigestFilePath { get; }

		/// <summary>
		/// Use the random index of the line to create digest faster. -1 is special value, it means the last character. If null then hash whole file.
		/// </summary>
		private int? DigestRandomIndex { get; }

		private const string DigestExtension = ".dig";

		/// <param name="digestRandomIndex">Use the random index of the line to create digest faster. -1 is special value, it means the last character. If null then hash whole file.</param>
		public DigestableSafeMutexIoManager(string filePath, int? digestRandomIndex = null) : base(filePath)

		{
			DigestRandomIndex = digestRandomIndex;

			DigestFilePath = $"{FilePath}{DigestExtension}";
		}

		#region IoOperations

		public new void DeleteMe()
		{
			base.DeleteMe();

			if (File.Exists(DigestFilePath))
			{
				File.Delete(DigestFilePath);
			}
		}

		public new async Task WriteAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
		{
			if (lines is null || !lines.Any())
			{
				return;
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

			await base.WriteAllLinesAsync(lines, cancellationToken);

			await WriteOutHashAsync(res.hash);
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

		#endregion IoOperations

		#region Hashing

		private async Task WriteOutHashAsync(byte[] hash)
		{
			try
			{
				IoHelpers.EnsureContainingDirectoryExists(DigestFilePath);

				await File.WriteAllBytesAsync(DigestFilePath, hash);
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Failed to create digest.");
				Logger.LogInfo(ex);
			}
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
				Logger.LogWarning("Failed to read digest.");
				Logger.LogInfo(ex);
			}

			return (false, hash);
		}

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
					int index = DigestRandomIndex == -1 || DigestRandomIndex >= line.Length // Last char.
						? line.Length - 1
						: DigestRandomIndex.Value;

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

		#endregion Hashing
	}
}

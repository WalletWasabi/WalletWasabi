using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Io;

/// <summary>
/// Safely manager file operations.
/// </summary>
public class DigestableSafeIoManager : SafeIoManager
{
	private const string DigestExtension = ".dig";

	/// <param name="digestRandomIndex">Use the random index of the line to create digest faster. -1 is special value, it means the last character. If null then hash whole file.</param>
	public DigestableSafeIoManager(string filePath, int? digestRandomIndex = null) : base(filePath)
	{
		DigestRandomIndex = digestRandomIndex;

		DigestFilePath = $"{FilePath}{DigestExtension}";
	}

	public string DigestFilePath { get; }

	/// <summary>
	/// Gets a random index of the line to create digest faster. -1 is special value, it means the last character. If null then hash whole file.
	/// </summary>
	private int? DigestRandomIndex { get; }

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

		var (same, hash) = await WorkWithHashAsync(byteArrayBuilder, cancellationToken).ConfigureAwait(false);
		if (same)
		{
			return;
		}

		await base.WriteAllLinesAsync(lines, cancellationToken).ConfigureAwait(false);

		await WriteOutHashAsync(hash).ConfigureAwait(false);
	}

	public new async Task AppendAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
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
				string? line = null;
				while (lineTask is { })
				{
					line ??= await lineTask.ConfigureAwait(false);

					lineTask = sr.EndOfStream ? null : sr.ReadLineAsync();

					// If the line is a line we want to write, then we know that someone else have worked into the file.
					if (linesArray[linesIndex] == line)
					{
						linesIndex++;
						continue;
					}

					await wTask.ConfigureAwait(false);
					wTask = sw.WriteLineAsync(line);

					ContinueBuildHash(byteArrayBuilder, line);

					cancellationToken.ThrowIfCancellationRequested();

					line = null;
				}
				await wTask.ConfigureAwait(false);
			}
			await sw.FlushAsync().ConfigureAwait(false);

			// 2. Then append.
			foreach (var line in linesArray)
			{
				await sw.WriteLineAsync(line).ConfigureAwait(false);

				ContinueBuildHash(byteArrayBuilder, line);

				cancellationToken.ThrowIfCancellationRequested();
			}

			await sw.FlushAsync().ConfigureAwait(false);
		}

		var (same, hash) = await WorkWithHashAsync(byteArrayBuilder, cancellationToken).ConfigureAwait(false);
		if (same)
		{
			return;
		}

		SafeMoveNewToOriginal();
		await WriteOutHashAsync(hash).ConfigureAwait(false);
	}

	#endregion IoOperations

	#region Hashing

	private async Task WriteOutHashAsync(byte[] hash)
	{
		try
		{
			IoHelpers.EnsureContainingDirectoryExists(DigestFilePath);

			await File.WriteAllBytesAsync(DigestFilePath, hash).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogWarning("Failed to create digest.");
			Logger.LogInfo(ex);
		}
	}

	private async Task<(bool same, byte[] hash)> WorkWithHashAsync(ByteArrayBuilder byteArrayBuilder, CancellationToken cancellationToken)
	{
		var hash = HashHelpers.GenerateSha256Hash(byteArrayBuilder.ToArray());
		try
		{
			if (File.Exists(DigestFilePath))
			{
				var digest = await File.ReadAllBytesAsync(DigestFilePath, cancellationToken).ConfigureAwait(false);
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

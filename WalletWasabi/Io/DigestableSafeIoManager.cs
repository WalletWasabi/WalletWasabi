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

	public DigestableSafeIoManager(string filePath, bool useLastCharacterDigest = false) : base(filePath)
	{
		UseLastCharacterDigest = useLastCharacterDigest;

		DigestFilePath = $"{FilePath}{DigestExtension}";
	}

	public string DigestFilePath { get; }

	private bool UseLastCharacterDigest { get; }

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
		if (!lines.Any())
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
		if (!lines.Any())
		{
			return;
		}

		IoHelpers.EnsureContainingDirectoryExists(NewFilePath);
		if (File.Exists(NewFilePath))
		{
			File.Delete(NewFilePath);
		}

		ByteArrayBuilder byteArrayBuilder = new();
		string[] linesArray = lines.ToArray();

		// 1. First copy.
		File.Copy(GetSafestFilePath(), NewFilePath, overwrite: true);

		// 2. Compute digest.				
		using (StreamReader sr = OpenText())
		{
			while (true)
			{
				string? line = sr.ReadLine();


				// End of stream.
				if (line is null)
				{
					break;
				}

				ContinueBuildHash(byteArrayBuilder, line);
				cancellationToken.ThrowIfCancellationRequested();
			}
		}

		// 3. Then append.
		using (FileStream fs = File.Open(NewFilePath, FileMode.Append))
		using (StreamWriter sw = new(fs, Encoding.ASCII, Constants.BigFileReadWriteBufferSize))
		{
			foreach (string line in linesArray)
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
			if (UseLastCharacterDigest)
			{
				char c = line[^1];
				byteArrayBuilder.Append((byte)c);
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

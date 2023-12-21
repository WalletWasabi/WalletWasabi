using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Io;

public class IoManager
{
	public IoManager(string filePath)
	{
		FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
		FileName = Path.GetFileName(FilePath);
		FileNameWithoutExtension = Path.GetFileNameWithoutExtension(FilePath);
	}

	public string FilePath { get; }

	public string FileName { get; }
	public string FileNameWithoutExtension { get; }

	#region IoOperations

	public void DeleteMe()
	{
		if (File.Exists(FilePath))
		{
			File.Delete(FilePath);
		}
	}

	public bool Exists()
	{
		return File.Exists(FilePath);
	}

	public async Task<string[]> ReadAllLinesAsync(CancellationToken cancellationToken = default)
	{
		return await ReadAllLinesAsync(FilePath, cancellationToken).ConfigureAwait(false);
	}

	protected static async Task<string[]> ReadAllLinesAsync(string filePath, CancellationToken cancellationToken)
	{
		return await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
	}

	protected static StreamReader OpenText(string filePath, int bufferSize = Constants.BigFileReadWriteBufferSize)
	{
		var fs = File.OpenRead(filePath);
		return new StreamReader(fs, Encoding.ASCII, detectEncodingFromByteOrderMarks: true, bufferSize: bufferSize, leaveOpen: false);
	}

	public async Task WriteAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
	{
		if (lines is null)
		{
			return;
		}

		IoHelpers.EnsureContainingDirectoryExists(FilePath);

		await File.WriteAllLinesAsync(FilePath, lines, cancellationToken).ConfigureAwait(false);
	}

	public async Task AppendAllLinesAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
	{
		if (!lines.Any())
		{
			return;
		}

		IoHelpers.EnsureContainingDirectoryExists(FilePath);

		await File.AppendAllLinesAsync(FilePath, lines, cancellationToken).ConfigureAwait(false);
	}

	#endregion IoOperations
}

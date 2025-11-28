using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Io;

public class SafeIoManager
{
	private const string OldExtension = ".old";
	private const string NewExtension = ".new";

	public SafeIoManager(string filePath)
	{
		FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
		FileName = Path.GetFileName(FilePath);
		FileNameWithoutExtension = Path.GetFileNameWithoutExtension(FilePath);
		OldFilePath = $"{FilePath}{OldExtension}";

		NewFilePath = $"{FilePath}{NewExtension}";
	}

	public string FilePath { get; }

	public string FileName { get; }
	public string FileNameWithoutExtension { get; }
	public string OldFilePath { get; }
	public string NewFilePath { get; }

	#region IoOperations

	public new void DeleteMe()
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
	}

	public bool TryReplaceMeWith(string sourcePath)
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
	/// Source: https://stackoverflow.com/questions/7957544/how-to-ensure-that-data-doesnt-get-corrupted-when-saving-to-file/7957634#7957634
	/// </summary>
	private bool TryGetSafestFileVersion([NotNullWhen(true)] out string? safestFilePath)
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
		if (!lines.Any())
		{
			return;
		}

		IoHelpers.EnsureContainingDirectoryExists(NewFilePath);

		await File.WriteAllLinesAsync(NewFilePath, lines, cancellationToken).ConfigureAwait(false);
		SafeMoveNewToOriginal();
	}

	public void WriteAllText(string text, Encoding encoding)
	{
		if (string.IsNullOrEmpty(text))
		{
			throw new ArgumentNullException(nameof(text), "Parameter cannot be null or empty.");
		}

		IoHelpers.EnsureContainingDirectoryExists(NewFilePath);

		File.WriteAllText(NewFilePath, text, encoding);
		SafeMoveNewToOriginal();
	}

	public async Task<string[]> ReadAllLinesAsync(CancellationToken cancellationToken = default)
	{
		return await File.ReadAllLinesAsync(GetSafestFilePath(), cancellationToken).ConfigureAwait(false);
	}

	public string ReadAllText(Encoding encoding)
	{
		return File.ReadAllText(GetSafestFilePath(), encoding);
	}

	public string GetSafestFilePath()
	{
		string filePath = FilePath;

		if (TryGetSafestFileVersion(out string? safestFilePath))
		{
			filePath = safestFilePath;
		}

		return filePath;
	}

	#endregion IoOperations
}

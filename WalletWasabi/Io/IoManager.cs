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

	protected static async Task<string[]> ReadAllLinesAsync(string filePath, CancellationToken cancellationToken)
	{
		return await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
	}

	#endregion IoOperations
}

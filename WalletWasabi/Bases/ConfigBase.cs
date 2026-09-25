using System.IO;
using System.Text;

namespace WalletWasabi.Bases;

public abstract class ConfigBase : NotifyPropertyChangedBase
{
	protected ConfigBase(string filePath)
	{
		FilePath = filePath;
	}

	private readonly Lock _fileLock = new();

	public string FilePath { get; }

	public void ToFile()
	{
		lock (_fileLock)
		{
			// Write-then-rename, so a crash or a concurrent reader never sees a truncated file.
			var tempFilePath = $"{FilePath}.tmp";
			File.WriteAllText(tempFilePath, EncodeAsJson(), Encoding.UTF8);
			File.Move(tempFilePath, FilePath, overwrite: true);
		}
	}

	protected abstract string EncodeAsJson();
}

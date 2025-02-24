using System.IO;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Bases;

public abstract class ConfigBase : NotifyPropertyChangedBase
{
	protected ConfigBase(string filePath)
	{
		FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
	}

	private readonly object _fileLock = new();

	public string FilePath { get; }

	public void ToFile()
	{
		lock (_fileLock)
		{
			File.WriteAllText(FilePath, EncodeAsJson(), Encoding.UTF8);
		}
	}

	protected abstract string EncodeAsJson();
}

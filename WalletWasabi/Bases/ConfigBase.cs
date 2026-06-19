using System.IO;
using System.Text;
using System.Threading;

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
			File.WriteAllText(FilePath, EncodeAsJson(), Encoding.UTF8);
		}
	}

	protected abstract string EncodeAsJson();
}

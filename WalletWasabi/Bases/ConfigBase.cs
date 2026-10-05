using System.Collections.Concurrent;
using System.IO;
using System.Text;
using WalletWasabi.Io;

namespace WalletWasabi.Bases;

public abstract class ConfigBase : NotifyPropertyChangedBase
{
	protected ConfigBase(string filePath)
	{
		FilePath = filePath;
	}

	// Reactive saves and readers can belong to different instances of the same file.
	private static readonly ConcurrentDictionary<string, Lock> FileLocks = new(
		OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

	protected static Lock GetFileLock(string filePath) => FileLocks.GetOrAdd(Path.GetFullPath(filePath), _ => new Lock());

	public string FilePath { get; }

	public void ToFile()
	{
		lock (GetFileLock(FilePath))
		{
			File.SafelyWriteAllText(FilePath, EncodeAsJson(), Encoding.UTF8);
		}
	}

	protected abstract string EncodeAsJson();
}

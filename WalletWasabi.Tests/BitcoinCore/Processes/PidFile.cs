using System.IO;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Tests.BitcoinCore.Processes;

/// <summary>
/// This class is useful to create a PID file for a process.
/// </summary>
/// <remarks>PID file is simply a file containing process ID as an integer.</remarks>
public class PidFile
{
	/// <summary>
	/// Creates new instance of <see cref="PidFile"/>.
	/// </summary>
	/// <param name="dataDir">Path to location where to store PID file.</param>
	/// <param name="pidFileName">File name.</param>
	public PidFile(string dataDir, string pidFileName)
	{
		string checkedDataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
		IoHelpers.EnsureDirectoryExists(checkedDataDir);

		FilePath = Path.Combine(checkedDataDir, pidFileName);
	}

	/// <summary>
	/// Full path to PID file.
	/// </summary>
	public string FilePath { get; }

	/// <summary>
	/// Attempts to read PID from <see cref="FilePath"/>.
	/// </summary>
	/// <returns>Process ID if the file still exists.</returns>
	public async Task<int?> TryReadAsync()
	{
		if (!File.Exists(FilePath))
		{
			return null;
		}

		var pidString = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
		return int.Parse(pidString);
	}

	/// <summary>
	/// Writes <paramref name="pid"/> to <see cref="FilePath"/>.
	/// </summary>
	/// <param name="pid">Process ID.</param>
	public async Task WriteFileAsync(int pid)
	{
		await File.WriteAllTextAsync(FilePath, pid.ToString()).ConfigureAwait(false);
	}

	/// <summary>
	/// Tries to delete PID file, if it still exists.
	/// </summary>
	public void TryDelete()
	{
		if (File.Exists(FilePath))
		{
			try
			{
				File.Delete(FilePath);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}
	}
}

using System;
using System.IO;
using WalletWasabi.Logging;

namespace WalletWasabi.Daemon;

public class SingleInstanceChecker : IDisposable
{
	public SingleInstanceChecker(string dataDirectory)
	{
		LockFilePath = Path.Combine(dataDirectory, ".wasabi-lock");
	}

	private FileStream? _lockFileStream;
	public string LockFilePath { get; }

	/// <summary>
	/// This function verifies whether is the only instance running on this machine and the given directory or not.
	/// </summary>
	/// <returns><c>true</c> if this is the first instance, <c>false</c> if another instance is running.</returns>
	public bool IsFirstInstance()
	{
		try
		{
			// FileShare.None is the key detail.
			_lockFileStream = new FileStream(
				LockFilePath,
				FileMode.OpenOrCreate,
				FileAccess.ReadWrite,
				FileShare.None,
				bufferSize: 1,
				FileOptions.DeleteOnClose
			);

			using var writer = new StreamWriter(_lockFileStream, leaveOpen: true);
			writer.WriteLine($"PID: {Environment.ProcessId}  Started: {DateTime.UtcNow:o}");
			writer.Flush();

			return true;
		}
		catch (IOException e)
		{
			if (!File.Exists(LockFilePath))
			{
				Logger.LogError($"Attempt to create the lock file '{LockFilePath}' failed with exception.", e);
			}

			return false;
		}
	}

	public void Dispose()
	{
		_lockFileStream?.Dispose();
		_lockFileStream = null;
	}
}

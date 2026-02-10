using System.IO;
using System.Text.Json;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class CoordinatorPrison
{
	private readonly string? _filePath;
	private BannedCoordinatorRecord? _bannedCoordinator;
	private readonly object _lock = new();

	private CoordinatorPrison(string? filePath, BannedCoordinatorRecord? bannedCoordinator)
	{
		_filePath = filePath;
		_bannedCoordinator = bannedCoordinator;
	}

	public bool IsBanned(string coordinatorHost)
	{
		lock (_lock)
		{
			return _bannedCoordinator is not null
				&& string.Equals(_bannedCoordinator.CoordinatorUri, coordinatorHost, StringComparison.OrdinalIgnoreCase);
		}
	}

	public void Ban(string coordinatorHost, string reason)
	{
		lock (_lock)
		{
			if (_bannedCoordinator is not null)
			{
				return;
			}

			_bannedCoordinator = new BannedCoordinatorRecord(coordinatorHost, DateTimeOffset.UtcNow, reason);
			Logger.LogError($"Coordinator '{coordinatorHost}' has been permanently banned. Reason: {reason}");
			ToFile();
		}
	}

	public static CoordinatorPrison CreateOrLoadFromFile(string containingDirectory)
	{
		string filePath = Path.Combine(containingDirectory, "BannedCoordinator.json");
		try
		{
			IoHelpers.EnsureFileExists(filePath);

			string data = File.ReadAllText(filePath);
			if (string.IsNullOrWhiteSpace(data))
			{
				return new CoordinatorPrison(filePath, null);
			}

			var record = JsonDecoder.FromString(data, Decode.BannedCoordinatorRecord);
			return new CoordinatorPrison(filePath, record);
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(CoordinatorPrison)}. Deleting corrupt file.", exc);
			File.Delete(filePath);
			return new CoordinatorPrison(filePath, null);
		}
	}

	private void ToFile()
	{
		if (string.IsNullOrWhiteSpace(_filePath) || _bannedCoordinator is null)
		{
			return;
		}

		IoHelpers.EnsureFileExists(_filePath);
		string json = JsonEncoder.ToReadableString(_bannedCoordinator, Encode.BannedCoordinatorRecord);
		File.WriteAllText(_filePath, json);
	}
}

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.WabiSabi.Client.Banning;

public class CoordinatorPrison
{
	private readonly string _filePath;
	private readonly List<BannedCoordinatorRecord> _bannedCoordinators;
	private readonly object _lock = new();

	private CoordinatorPrison(string filePath, List<BannedCoordinatorRecord> bannedCoordinators)
	{
		_filePath = filePath;
		_bannedCoordinators = bannedCoordinators;
	}

	public bool IsBanned(string coordinatorHost, [NotNullWhen(true)] out string? reason)
	{
		lock (_lock)
		{
			var record = _bannedCoordinators.FirstOrDefault(r =>
				string.Equals(r.CoordinatorHost, coordinatorHost, StringComparison.OrdinalIgnoreCase));

			if (record is not null)
			{
				reason = record.Reason;
				return true;
			}

			reason = null;
			return false;
		}
	}

	public void Ban(string coordinatorHost, string reason)
	{
		lock (_lock)
		{
			if (_bannedCoordinators.Any(r =>
				string.Equals(r.CoordinatorHost, coordinatorHost, StringComparison.OrdinalIgnoreCase)))
			{
				return;
			}

			var record = new BannedCoordinatorRecord(coordinatorHost, DateTimeOffset.UtcNow, reason);
			_bannedCoordinators.Add(record);
			Logger.LogError($"Coordinator '{coordinatorHost}' has been permanently banned. Reason: {reason}");
			Logger.LogError($"Modify '{_filePath}' to unban the coordinator, if you wish so.");
			ToFileLocked();
		}
	}

	public static CoordinatorPrison CreateOrLoadFromFile(string containingDirectory)
	{
		string filePath = Path.Combine(containingDirectory, "BannedCoordinators.json");
		try
		{
			IoHelpers.EnsureFileExists(filePath);

			string data = File.ReadAllText(filePath);
			if (string.IsNullOrWhiteSpace(data))
			{
				return new CoordinatorPrison(filePath, []);
			}

			var records = JsonDecoder.FromString(data, Decode.Array(Decode.BannedCoordinatorRecord))
				?? throw new InvalidDataException("Banned coordinators file is corrupted.");

			return new CoordinatorPrison(filePath, records.ToList());
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(CoordinatorPrison)}. Deleting corrupted file '{filePath}'.", exc);
			File.Delete(filePath);
			return new CoordinatorPrison(filePath, []);
		}
	}

	private void ToFileLocked()
	{
		IoHelpers.EnsureFileExists(_filePath);
		string json = JsonEncoder.ToReadableString(_bannedCoordinators, Encode.CoordinatorPrison);
		File.WriteAllText(_filePath, json);
	}
}

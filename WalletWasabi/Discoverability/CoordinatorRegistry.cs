using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.Discoverability;

public class CoordinatorRegistry
{
	private readonly string _filePath;
	private readonly Dictionary<string, DateTimeOffset> _firstSeen;

	private CoordinatorRegistry(string filePath, Dictionary<string, DateTimeOffset> firstSeen)
	{
		_filePath = filePath;
		_firstSeen = firstSeen;
	}

	public DateTimeOffset GetFirstSeen(string pubKey) => _firstSeen[pubKey];

	public void Register(IEnumerable<string> pubKeys, DateTimeOffset now)
	{
		var changed = false;
		foreach (var pubKey in pubKeys)
		{
			if (_firstSeen.TryAdd(pubKey, now))
			{
				changed = true;
			}
		}

		if (changed)
		{
			Save();
		}
	}

	public static CoordinatorRegistry CreateOrLoadFromFile(string containingDirectory)
	{
		var filePath = Path.Combine(containingDirectory, "KnownCoordinators.json");
		try
		{
			IoHelpers.EnsureFileExists(filePath);
			var data = SafeFile.ReadAllText(filePath, Encoding.UTF8);
			if (string.IsNullOrWhiteSpace(data))
			{
				return new CoordinatorRegistry(filePath, []);
			}

			var firstSeen = JsonDecoder.FromString(data, Decode.Dictionary(Decode.DateTimeOffset))
				?? throw new InvalidDataException("Known coordinators file is corrupted.");
			return new CoordinatorRegistry(filePath, firstSeen);
		}
		catch (Exception exc)
		{
			Logger.LogError($"There was an error during loading {nameof(CoordinatorRegistry)}. Deleting corrupt file.", exc);
			File.Delete(filePath);
			return new CoordinatorRegistry(filePath, []);
		}
	}

	private void Save()
	{
		try
		{
			var json = JsonEncoder.ToReadableString(_firstSeen,
				d => Encode.Dictionary(d.ToDictionary(kv => kv.Key, kv => Encode.DatetimeOffset(kv.Value))));
			SafeFile.WriteAllText(_filePath, json, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Failed to save {nameof(CoordinatorRegistry)}: {ex.Message}");
		}
	}
}

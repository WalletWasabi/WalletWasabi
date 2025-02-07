using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Bases;

public class ConfigManager
{
	private static readonly JsonSerializer Serializer = JsonSerializer.Create(JsonSerializationOptions.Default.Settings);

	public static string ToFile<T>(string filePath, T obj)
	{
		string jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSerializationOptions.Default.Settings);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);

		return jsonString;
	}

	public static bool AreDeepEqual(object current, object other)
	{
		JObject currentConfig = JObject.FromObject(current, Serializer);
		JObject otherConfigJson = JObject.FromObject(other, Serializer);
		return JToken.DeepEquals(otherConfigJson, currentConfig);
	}

	/// <summary>
	/// Check if the config file differs from the config if the file path of the config file is set, otherwise throw exception.
	/// </summary>
	public static bool CheckFileChange<T>(string filePath, T current)
		where T : IConfig, new()
	{
		T diskVersion = LoadFile<T>(filePath);
		return !AreDeepEqual(diskVersion, current);
	}

	private static TResponse LoadFile<TResponse>(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"File '{filePath}' does not exist.");
		}

		string jsonString = File.ReadAllText(filePath, Encoding.UTF8);

		TResponse? result = JsonConvert.DeserializeObject<TResponse>(jsonString, JsonSerializationOptions.Default.Settings);

		return result is not null
			? result
			: throw new JsonException("Unexpected null value.");
	}

	public static TResponse LoadFile<TResponse>(string filePath, bool createIfMissing = false)
		where TResponse : new()
	{
		TResponse result;

		if (!createIfMissing)
		{
			return LoadFile<TResponse>(filePath);
		}

		if (!File.Exists(filePath))
		{
			Logger.LogInfo($"File did not exist. Created at path: '{filePath}'.");
			result = new();
			ToFile(filePath, result);
		}
		else
		{
			try
			{
				return LoadFile<TResponse>(filePath);
			}
			catch (Exception ex)
			{
				result = new();
				ToFile(filePath, result);

				Logger.LogInfo($"File has been deleted because it was corrupted. Recreated default version at path: '{filePath}'.");
				Logger.LogWarning(ex);
			}
		}

		return result;
	}
}

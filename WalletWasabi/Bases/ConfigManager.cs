using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases;

public class ConfigManager
{
	public static string ToFile<T, TSerializationOptions>(string filePath, T obj, TSerializationOptions options)
	{
		string jsonString = Serialize(obj, options);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);
		return jsonString;
	}

	private static string Serialize<T, TSerializationOptions>(T obj, TSerializationOptions options)
	{
		string jsonString;

		if (options is JsonSerializerOptions serializerOptions)
		{
			jsonString = System.Text.Json.JsonSerializer.Serialize<T>(obj, serializerOptions);
		}
		else if (options is JsonSerializerSettings serializerSettings)
		{
			jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented, serializerSettings);
		}
		else
		{
			throw new NotSupportedException();
		}

		return jsonString;
	}

	public static bool AreDeepEqual<TSerializationOptions>(object current, object other, TSerializationOptions options)
	{
		if (options is JsonSerializerOptions serializerOptions)
		{
			string currentConfig = Serialize(current, serializerOptions);
			string otherConfigJson = Serialize(other, serializerOptions);
			return currentConfig == otherConfigJson;
		}
		else if (options is JsonSerializerSettings serializerSettings)
		{
			Newtonsoft.Json.JsonSerializer serializer = Newtonsoft.Json.JsonSerializer.Create(serializerSettings);
			JObject currentConfig = JObject.FromObject(current, serializer);
			JObject otherConfigJson = JObject.FromObject(other, serializer);
			return JToken.DeepEquals(otherConfigJson, currentConfig);
		}

		throw new NotSupportedException();
	}

	/// <summary>
	/// Check if the config file differs from the config if the file path of the config file is set, otherwise throw exception.
	/// </summary>
	public static bool CheckFileChange<T, TSerializationOptions>(string filePath, T current, TSerializationOptions options)
		where T : IConfig, new()
	{
		T diskVersion = LoadFile<T, TSerializationOptions>(filePath, options);
		return !AreDeepEqual(diskVersion, current, options);
	}

	private static TResponse LoadFile<TResponse, TSerializationOptions>(string filePath, TSerializationOptions options)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"File '{filePath}' does not exist.");
		}

		string jsonString = File.ReadAllText(filePath, Encoding.UTF8);

		TResponse? result;

		if (options is JsonSerializerOptions serializerOptions)
		{
			result = System.Text.Json.JsonSerializer.Deserialize<TResponse>(jsonString, serializerOptions);
		}
		else if (options is JsonSerializerSettings serializerSettings)
		{
			result = JsonConvert.DeserializeObject<TResponse>(jsonString, serializerSettings);
		}
		else
		{
			throw new NotSupportedException();
		}

		return result is not null
			? result
			: throw new Newtonsoft.Json.JsonException("Unexpected null value.");
	}

	public static TResponse LoadFile<TResponse, TSerializationOptions>(string filePath, TSerializationOptions options, bool createIfMissing = false)
		where TResponse : IConfigNg, new()
	{
		TResponse result;

		if (!createIfMissing)
		{
			return LoadFile<TResponse, TSerializationOptions>(filePath, options);
		}

		if (!File.Exists(filePath))
		{
			Logger.LogInfo($"File did not exist. Created at path: '{filePath}'.");
			result = new();
			ToFile(filePath, result, options);
		}
		else
		{
			try
			{
				return LoadFile<TResponse, TSerializationOptions>(filePath, options);
			}
			catch (Exception ex)
			{
				result = new();
				ToFile(filePath, result, options);

				Logger.LogInfo($"File has been deleted because it was corrupted. Recreated default version at path: '{filePath}'.");
				Logger.LogWarning(ex);
			}
		}

		return result;
	}
}

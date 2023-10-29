using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using WalletWasabi.Interfaces;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.JsonConverters.Timing;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases;

public class ConfigManager
{
	private static readonly bool UseNg = true;

	static ConfigManager()
	{
		SerializerSettingsNg.Converters.Add(new NetworkJsonConverterNg());
		SerializerSettingsNg.Converters.Add(new FeeRateJsonConverterNg());
		SerializerSettingsNg.Converters.Add(new MoneySatoshiJsonConverterNg());
		SerializerSettingsNg.Converters.Add(new TimeSpanJsonConverterNg());
		SerializerSettingsNg.Converters.Add(new ExtPubKeyJsonConverterNg());
	}

	/// <remarks>Do not add converters that are not needed. It prolongs app's startup time.</remarks>
	private static readonly JsonSerializerSettings SerializerSettings = new()
	{
		Converters = new List<JsonConverter>()
			{
				new NetworkJsonConverter(),
				new FeeRateJsonConverter(),
				new MoneySatoshiJsonConverter(),
				new TimeSpanJsonConverter(),
				new ExtPubKeyJsonConverter(),
			}
	};

	/// <remarks>Do not add converters that are not needed. It prolongs app's startup time.</remarks>
	private static readonly JsonSerializerOptions SerializerSettingsNg = new();

	private static readonly Newtonsoft.Json.JsonSerializer Serializer = Newtonsoft.Json.JsonSerializer.Create(SerializerSettings);

	public static TResult LoadFile<TResult>(string filePath, bool createIfMissing = false)
		where TResult : IConfigNg, new()
	{
		TResult result;

		if (!createIfMissing)
		{
			return LoadFile<TResult>(filePath);
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
				return LoadFile<TResult>(filePath);
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

	public static void ToFile<T>(string filePath, T obj)
	{
		string jsonString = UseNg
			? JsonConvert.SerializeObject(obj, Formatting.Indented, SerializerSettings)
			: System.Text.Json.JsonSerializer.Serialize<T>(obj, SerializerSettingsNg);

		File.WriteAllText(filePath, jsonString, Encoding.UTF8);
	}

	protected static TResponse LoadFile<TResponse>(string filePath)
	{
		if (!File.Exists(filePath))
		{
			throw new FileNotFoundException($"File '{filePath}' does not exist.");
		}

		string jsonString = File.ReadAllText(filePath, Encoding.UTF8);

		TResponse? result = UseNg
			? System.Text.Json.JsonSerializer.Deserialize<TResponse>(jsonString, SerializerSettingsNg)
			: JsonConvert.DeserializeObject<TResponse>(jsonString, SerializerSettings);

		return result is not null
			? result
			: throw new Newtonsoft.Json.JsonException("Unexpected null value.");
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
}

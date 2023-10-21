using Newtonsoft.Json;
using System.IO;
using System.Text;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.Bases;

public class ConfigManager
{
	public static TResult LoadFile<TResult>(string filePath, bool createIfMissing = false)
		where TResult : new()
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

	/// <inheritdoc />
	public static void ToFile<T>(string filePath, T obj)
	{
		string jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented, JsonSerializationOptions.Default.Settings);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);
	}

	protected static TResponse LoadFile<TResponse>(string filePath)
	{
		string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
		TResponse? result = JsonConvert.DeserializeObject<TResponse>(jsonString, JsonSerializationOptions.Default.Settings);

		return result is not null
			? result
			: throw new JsonException("Unexpected null value.");
	}
}

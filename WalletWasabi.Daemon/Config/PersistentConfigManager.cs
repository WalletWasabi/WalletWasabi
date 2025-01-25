using System;
using System.IO;
using System.Text;
using System.Text.Json;
using WalletWasabi.Daemon;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.Bases;

public static class PersistentConfigManager
{
	public static readonly JsonSerializerOptions DefaultOptions = new()
	{
		WriteIndented = true,
	};

	public static string ToFile(string filePath, PersistentConfig obj)
	{
		string jsonString = PersistentConfigEncode.PersistentConfig(obj).ToJsonString(DefaultOptions);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);
		return jsonString;
	}

	public static PersistentConfig LoadFile(string filePath)
	{
		try
		{
			using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
			var decoder = Decode.FromStream(PersistentConfigDecode.PersistentConfig);
			var decodingResult = decoder(cfgFile);
			return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
		}
		catch (Exception ex)
		{
			var config = new PersistentConfig();
			File.WriteAllTextAsync(filePath, PersistentConfigEncode.PersistentConfig(config).ToJsonString());
			Logger.LogInfo($"{nameof(Config)} file has been deleted because it was corrupted. Recreated default version at path: `{filePath}`.");
			Logger.LogWarning(ex);
			return config;
		}

	}
}

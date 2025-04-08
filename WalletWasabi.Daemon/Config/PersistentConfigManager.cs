using System;
using System.IO;
using System.Text;
using WalletWasabi.Daemon;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;

namespace WalletWasabi.Bases;

public static class PersistentConfigManager
{
	public static string ToFile(string filePath, PersistentConfig obj)
	{
		string jsonString = JsonEncoder.ToReadableString(obj, PersistentConfigEncode.PersistentConfig);
		File.WriteAllText(filePath, jsonString, Encoding.UTF8);
		return jsonString;
	}

	public static IPersistentConfig LoadFile(string filePath)
	{
		try
		{
			using var cfgFile = File.Open(filePath, FileMode.Open, FileAccess.Read);
			var decoder = JsonDecoder.FromStream(PersistentConfigDecode.PersistentConfig);
			var decodingResult = decoder(cfgFile);
			return decodingResult.Match(cfg => cfg, error => throw new InvalidOperationException(error));
		}
		catch (Exception ex)
		{
			var config = new PersistentConfig();
			File.WriteAllTextAsync(filePath, JsonEncoder.ToReadableString(config, PersistentConfigEncode.PersistentConfig));
			Logger.LogInfo($"{nameof(Config)} file has been deleted because it was corrupted. Recreated default version at path: `{filePath}`.");
			Logger.LogWarning(ex);
			return config;
		}
	}
}

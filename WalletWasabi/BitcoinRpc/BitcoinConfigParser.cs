using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Extensions;

namespace WalletWasabi.BitcoinRpc;

public class BitcoinConfig
{
	private readonly IDictionary<string,Setting[]> _settings;

	record Setting(string Network, string Key, string Value);

	private BitcoinConfig(IDictionary<string, Setting[]> settings)
	{
		_settings = settings;
	}

	public static BitcoinConfig Parse(string configString) => new(
		ParseConfigFile(configString)
			.DistinctBy(x => $"{x.Network}.{x.Setting}")
			.GroupBy(x => x.Setting)
			.ToDictionary(x => x.Key, x => x.Select(y => new Setting(y.Network, y.Setting, y.Value)).ToArray()));

	public string? GetSettingOrNull(string key, string network)
	{
		if (_settings.TryGetValue(key, out var settings))
		{
			var setting =
				settings.FirstOrDefault(x => x.Network == network) ??
				settings.FirstOrDefault(x => x.Network == "global");
			return setting?.Value;
		}
		return null;
	}


	private static IEnumerable<(string Network, string Setting, string Value)> ParseConfigFile(string configString)
	{
		var currentNetwork = "global";

		foreach (var line in configString.SplitLines())
		{
			if (line is ['[', ..var newNetwork, ']'])
			{
				currentNetwork = newNetwork.ToLowerInvariant();
				continue;
			}

			var parts = line.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			if (parts is [var setting, var value])
			{
				var settingParts = setting.Split('.', StringSplitOptions.TrimEntries);
				if (settingParts is [var network, var networkSetting])
				{
					yield return (network, networkSetting, value);
				}
				else if (settingParts is [var globalSetting])
				{
					 yield return (currentNetwork, globalSetting, value);
				}
			}
		}
	}
}

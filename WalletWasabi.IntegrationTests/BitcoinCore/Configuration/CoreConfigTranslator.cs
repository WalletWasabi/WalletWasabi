using System.Linq;
using NBitcoin;
using WalletWasabi.IntegrationTests.BitcoinCore.Configuration.Whitening;

namespace WalletWasabi.IntegrationTests.BitcoinCore.Configuration;

public class CoreConfigTranslator
{
	public CoreConfigTranslator(CoreConfig config, Network network)
	{
		Config = config;
		Network = network;
	}

	public CoreConfig Config { get; }
	public Network Network { get; }

	public string? TryGetValue(string key)
	{
		var configDic = Config.ToDictionary();
		return NetworkTranslator.GetConfigPrefixesWithDots(Network)
			.Select(networkPrefixWithDot => configDic.TryGet($"{networkPrefixWithDot}{key}"))
			.LastOrDefault(x => x is { });
	}

	public string? TryGetRpcUser() => TryGetValue("rpcuser");

	public string? TryGetRpcPassword() => TryGetValue("rpcpassword");

	public string? TryGetRpcCookieFile() => TryGetValue("rpccookiefile");

	public string? TryGetRpcBind() => TryGetValue("rpcbind");

	public ushort? TryGetRpcPort()
	{
		var stringValue = TryGetValue("rpcport");
		if (stringValue is { } && ushort.TryParse(stringValue, out ushort value))
		{
			return value;
		}

		return null;
	}

	public ushort? TryGetPort()
	{
		var stringValue = TryGetValue("port");
		if (stringValue is { } && ushort.TryParse(stringValue, out ushort value))
		{
			return value;
		}

		return null;
	}

	public WhiteBind? TryGetWhiteBind()
	{
		var stringValue = TryGetValue("whitebind");
		if (stringValue is { } && WhiteBind.TryParse(stringValue, Network, out WhiteBind? value))
		{
			return value;
		}

		return null;
	}
}

using System.Linq;
using NBitcoin;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.BitcoinCore.Configuration.Whitening;

namespace WalletWasabi.Tests.BitcoinCore.Configuration;

public class CoreConfigTranslator
{
	public CoreConfigTranslator(CoreConfig config, Network network)
	{
		Config = Guard.NotNull(nameof(config), config);
		Network = Guard.NotNull(nameof(network), network);
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

	public WhiteBind? TryGetWhiteBind()
	{
		var stringValue = TryGetValue("whitebind");
		if (stringValue is { } && WhiteBind.TryParse(stringValue, out WhiteBind? value))
		{
			return value;
		}

		return null;
	}
}

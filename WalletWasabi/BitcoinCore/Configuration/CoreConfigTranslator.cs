using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.BitcoinCore.Configuration.Whitening;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Configuration
{
	public class CoreConfigTranslator
	{
		public CoreConfig Config { get; }
		public Network Network { get; }

		public CoreConfigTranslator(CoreConfig config, Network network)
		{
			Config = Guard.NotNull(nameof(config), config);
			Network = Guard.NotNull(nameof(network), network);
		}

		public string TryGetValue(string key)
		{
			var configDic = Config.ToDictionary();
			string result = null;
			foreach (var networkPrefixWithDot in NetworkTranslator.GetConfigPrefixesWithDots(Network))
			{
				var found = configDic.TryGet($"{networkPrefixWithDot}{key}");
				if (found is { })
				{
					result = found;
				}
			}

			return result;
		}

		public string TryGetRpcUser() => TryGetValue("rpcuser");

		public string TryGetRpcPassword() => TryGetValue("rpcpassword");

		public string TryGetRpcCookieFile() => TryGetValue("rpccookiefile");

		public string TryGetRpcHost() => TryGetValue("rpchost");

		public ushort? TryGetRpcPort()
		{
			var stringValue = TryGetValue("rpcport");
			if (stringValue is { } && ushort.TryParse(stringValue, out ushort value))
			{
				return value;
			}

			return null;
		}

		public WhiteBind TryGetWhiteBind()
		{
			var stringValue = TryGetValue("whitebind");
			if (stringValue is { } && WhiteBind.TryParse(stringValue, Network, out WhiteBind value))
			{
				return value;
			}

			return null;
		}

		public WhiteList TryGetWhiteList()
		{
			var stringValue = TryGetValue("whitelist");
			if (stringValue is { } && WhiteList.TryParse(stringValue, Network, out WhiteList value))
			{
				return value;
			}

			return null;
		}
	}
}

using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Configuration
{
	public static class NetworkTranslator
	{
		public static string GetConfigPrefix(Network network, bool mainnetEmpty)
		{
			Guard.NotNull(nameof(network), network);
			if (network == Network.Main)
			{
				if (mainnetEmpty)
				{
					return string.Empty;
				}
				else
				{
					return "main";
				}
			}
			else if (network == Network.TestNet)
			{
				return "test";
			}
			else if (network == Network.RegTest)
			{
				return "regtest";
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}

		public static string GetDataDirPrefix(Network network)
		{
			Guard.NotNull(nameof(network), network);
			if (network == Network.Main)
			{
				return string.Empty;
			}
			else if (network == Network.TestNet)
			{
				return "testnet3";
			}
			else if (network == Network.RegTest)
			{
				return "regtest";
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}
	}
}

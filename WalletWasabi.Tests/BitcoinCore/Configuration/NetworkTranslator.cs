using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.BitcoinCore.Configuration;

public static class NetworkTranslator
{
	public static string GetConfigPrefix(Network network)
	{
		Guard.NotNull(nameof(network), network);
		if (network == Network.Main)
		{
			return "main";
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

	public static IEnumerable<string> GetConfigPrefixesWithDots(Network network)
	{
		Guard.NotNull(nameof(network), network);

		yield return $"{GetConfigPrefix(network)}.";
		if (network == Network.Main)
		{
			yield return "";
		}
	}

	public static string GetDataDirPrefix(Network network)
	{
		Guard.NotNull(nameof(network), network);
		if (network == Network.Main)
		{
			return "";
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

	public static string GetCommandLineArguments(Network network)
	{
		Guard.NotNull(nameof(network), network);
		if (network == Network.Main)
		{
			return "-regtest=0 -testnet=0";
		}
		else if (network == Network.TestNet)
		{
			return "-regtest=0 -testnet4=1";
		}
		else if (network == Network.RegTest)
		{
			return "-regtest=1 -testnet=0";
		}
		else
		{
			throw new NotSupportedNetworkException(network);
		}
	}
}

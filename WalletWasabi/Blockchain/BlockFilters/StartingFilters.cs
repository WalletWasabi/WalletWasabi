using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Exceptions;
using WalletWasabi.Models;

namespace WalletWasabi.Blockchain.BlockFilters
{
	public static class StartingFilters
	{
		public static FilterModel GetStartingFilter(Network network)
		{
			var startingHeader = SmartHeader.GetStartingHeader(network);
			if (network == Network.Main)
			{
				return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:02832810ec08a0:{startingHeader.PrevHash}:{startingHeader.BlockTime.ToUnixTimeSeconds()}");
			}
			else if (network == Network.TestNet)
			{
				return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a:{startingHeader.PrevHash}:{startingHeader.BlockTime.ToUnixTimeSeconds()}");
			}
			else if (network == Network.RegTest)
			{
				return FilterModel.FromLine($"{startingHeader.Height}:{startingHeader.BlockHash}:00:{startingHeader.PrevHash}:{startingHeader.BlockTime.ToUnixTimeSeconds()}");
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}
	}
}

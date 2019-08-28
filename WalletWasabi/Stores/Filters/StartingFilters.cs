using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Backend.Models;
using WalletWasabi.Models;

namespace WalletWasabi.Stores.Filters
{
	public static class StartingFilters
	{
		public static FilterModel GetStartingFilter(Network network)
		{
			if (network == Network.Main)
			{
				return FilterModel.FromHeightlessLine("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893:02832810ec08a0", GetStartingHeight(network));
			}
			else if (network == Network.TestNet)
			{
				return FilterModel.FromHeightlessLine("00000000000f0d5edcaeba823db17f366be49a80d91d15b77747c2e017b8c20a:017821b8", GetStartingHeight(network));
			}
			else if (network == Network.RegTest)
			{
				return FilterModel.FromHeightlessLine("0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206", GetStartingHeight(network));
			}
			else
			{
				throw new NotSupportedException($"{nameof(Network)} not supported: {network}.");
			}
		}

		public static Height GetStartingHeight(Network network) // First possible bech32 transaction ever.
		{
			if (network == Network.Main)
			{
				return new Height(481824);
			}
			else if (network == Network.TestNet)
			{
				return new Height(828575);
			}
			else if (network == Network.RegTest)
			{
				return new Height(0);
			}
			else
			{
				throw new NotSupportedException($"{nameof(Network)} not supported: {network}.");
			}
		}
	}
}

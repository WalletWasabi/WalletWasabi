using System;
using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers
{
	public static class TransactionFeeHelper
	{
		public static Dictionary<int, int> GetFeeEstimates(Wallet wallet)
		{
			if (wallet.FeeProvider.AllFeeEstimate is null)
			{
				throw new InvalidOperationException($"Not possible to get the fee estimates. {nameof(wallet.FeeProvider.AllFeeEstimate)} is null.");
			}

			return wallet.Network == Network.TestNet ? TestNetFeeEstimates : wallet.FeeProvider.AllFeeEstimate.Estimations;
		}

		private static readonly Dictionary<int, int> TestNetFeeEstimates = new()
		{
			[1] = 17,
			[2] = 12,
			[3] = 9,
			[6] = 9,
			[18] = 2,
			[36] = 2,
			[72] = 2,
			[144] = 2,
			[432] = 1,
			[1008] = 1
		};
	}
}

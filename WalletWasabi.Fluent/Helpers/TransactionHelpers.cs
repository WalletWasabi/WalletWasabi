using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Helpers
{
	public static class TransactionHelpers
	{
		public static BuildTransactionResult BuildTransaction(Wallet wallet, BitcoinAddress address, Money amount, SmartLabel labels, FeeRate feeRate, IEnumerable<SmartCoin> coins, bool subtractFee)
		{
			var intent = new PaymentIntent(
				destination: address,
				amount: amount,
				subtractFee: subtractFee,
				label: labels);

			var txRes = wallet.BuildTransaction(
				wallet.Kitchen.SaltSoup(),
				intent,
				FeeStrategy.CreateFromFeeRate(feeRate),
				allowUnconfirmed: true,
				coins.Select(coin => coin.OutPoint));

			return txRes;
		}
	}
}

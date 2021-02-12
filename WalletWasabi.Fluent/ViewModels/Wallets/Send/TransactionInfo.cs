using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public class TransactionInfo
	{
		public SmartLabel Labels { get; set; }

		public BitcoinAddress Address { get; set; }

		public Money Amount { get; set; }

		public FeeRate FeeRate { get; set; }

		public IEnumerable<SmartCoin> Coins { get; set; }
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public class TransactionInfo
	{
		public SmartLabel SendLabels { private get; set; }

		public SmartLabel Labels => SmartLabel.Merge(SendLabels, SmartLabel.Merge(Coins.Select(x => x.HdPubKey.Cluster.Labels)));

		public BitcoinAddress Address { get; set; }

		public Money Amount { get; set; }

		public FeeRate FeeRate { get; set; }

		public TimeSpan ConfirmationTimeSpan { get; set; }

		public IEnumerable<SmartCoin> Coins { get; set; }

		public IPayjoinClient? PayJoinClient { get; set; }
	}
}

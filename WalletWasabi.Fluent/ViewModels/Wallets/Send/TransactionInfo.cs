using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public class TransactionInfo
	{
		private readonly int _privateCoinThreshold;

		public TransactionInfo(Wallet wallet)
		{
			_privateCoinThreshold = wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();
		}

		public SmartLabel UserLabels { get; set; }

		public BitcoinAddress Address { get; set; }

		public Money Amount { get; set; }

		public FeeRate? FeeRate { get; set; }

		public TimeSpan ConfirmationTimeSpan { get; set; }

		public IEnumerable<SmartCoin> Coins { get; set; } = Enumerable.Empty<SmartCoin>();

		public IPayjoinClient? PayJoinClient { get; set; }

		public bool IsPayJoin => PayJoinClient is { };

		public bool IsPrivatePocketUsed => Coins.All(x => x.HdPubKey.AnonymitySet >= _privateCoinThreshold);

		public bool SubtractFee { get; set; }
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	public partial class TransactionInfo
	{
		private readonly int _privateCoinThreshold;

		[AutoNotify] private Money _amount = Money.Zero;

		public TransactionInfo(Wallet wallet)
		{
			_privateCoinThreshold = wallet.ServiceConfiguration.GetMixUntilAnonymitySetValue();

			this.WhenAnyValue(x => x.Amount)
				.Subscribe(_ => OnAmountChanged());
		}

		public SmartLabel UserLabels { get; set; } = SmartLabel.Empty;

		public BitcoinAddress Address { get; set; }

		public FeeRate? FeeRate { get; set; }

		public TimeSpan ConfirmationTimeSpan { get; set; }

		public IEnumerable<SmartCoin> Coins { get; set; } = Enumerable.Empty<SmartCoin>();

		public IPayjoinClient? PayJoinClient { get; set; }

		public bool IsPayJoin => PayJoinClient is { };

		public bool IsPrivatePocketUsed => Coins.All(x => x.HdPubKey.AnonymitySet >= _privateCoinThreshold);

		public bool SubtractFee { get; set; }

		private void OnAmountChanged()
		{
			SubtractFee = default;
			FeeRate = default;

			if (Coins.Sum(x => x.Amount) < Amount) // Reset coins if the selected cluster is not enough for the new amount
			{
				Coins = Enumerable.Empty<SmartCoin>();
			}
		}
	}
}

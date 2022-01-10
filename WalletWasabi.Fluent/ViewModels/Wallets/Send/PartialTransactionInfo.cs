using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class PartialTransactionInfo
{
	private readonly int _privateCoinThreshold;

	[AutoNotify] private Money _amount = Money.Zero;

	public PartialTransactionInfo()
	{
		_privateCoinThreshold = Services.Config.MinAnonScoreTarget;

		this.WhenAnyValue(x => x.Amount)
			.Subscribe(_ => OnAmountChanged());
	}

	public TransactionInfo ToFullTransaction(BitcoinAddress address)
	{
		return new TransactionInfo(address)
		{
			Amount = Amount,
			UserLabels = UserLabels,
			FeeRate = FeeRate,
			MaximumPossibleFeeRate = MaximumPossibleFeeRate,
			ConfirmationTimeSpan = ConfirmationTimeSpan,
			Coins = Coins,
			ChangelessCoins = ChangelessCoins,
			PayJoinClient = PayJoinClient,
			IsCustomFeeUsed = IsCustomFeeUsed,
			SubtractFee = SubtractFee
		};
	}

	public SmartLabel UserLabels { get; set; } = SmartLabel.Empty;

	public FeeRate FeeRate { get; set; } = FeeRate.Zero;

	public FeeRate? MaximumPossibleFeeRate { get; set; }

	public TimeSpan ConfirmationTimeSpan { get; set; }

	public IEnumerable<SmartCoin> Coins { get; set; } = Enumerable.Empty<SmartCoin>();

	public IEnumerable<SmartCoin> ChangelessCoins { get; set; } = Enumerable.Empty<SmartCoin>();

	public IPayjoinClient? PayJoinClient { get; set; }

	public bool IsPayJoin => PayJoinClient is { };

	public bool IsOptimized => ChangelessCoins.Any();

	public bool IsPrivate => Coins.All(x => x.HdPubKey.AnonymitySet >= _privateCoinThreshold);

	public bool IsCustomFeeUsed { get; set; }

	public bool SubtractFee { get; set; }

	private void OnAmountChanged()
	{
		SubtractFee = default;
		ChangelessCoins = Enumerable.Empty<SmartCoin>();
		MaximumPossibleFeeRate = null;

		if (!IsCustomFeeUsed)
		{
			FeeRate = FeeRate.Zero;
		}

		if (Coins.Sum(x => x.Amount) < Amount) // Reset coins if the selected cluster is not enough for the new amount
		{
			Coins = Enumerable.Empty<SmartCoin>();
		}
	}
}
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionInfo
{
	private readonly int _privateCoinThreshold;

	[AutoNotify] private FeeRate _feeRate = FeeRate.Zero;
	[AutoNotify] private IEnumerable<SmartCoin> _coins = Enumerable.Empty<SmartCoin>();

	public TransactionInfo(int minAnonScoreTarget)
	{
		_privateCoinThreshold = minAnonScoreTarget;

		this.WhenAnyValue(x => x.FeeRate)
			.Subscribe(_ => OnFeeChanged());

		this.WhenAnyValue(x => x.Coins)
			.Subscribe(_ => OnCoinsChanged());
	}

	public Money Amount { get; set; } = Money.Zero;

	public SmartLabel UserLabels { get; set; } = SmartLabel.Empty;

	public FeeRate? MaximumPossibleFeeRate { get; set; }

	public TimeSpan ConfirmationTimeSpan { get; set; }

	public IEnumerable<SmartCoin> ChangelessCoins { get; set; } = Enumerable.Empty<SmartCoin>();

	public IPayjoinClient? PayJoinClient { get; set; }

	public bool IsPayJoin => PayJoinClient is { };

	public bool IsOptimized => ChangelessCoins.Any();

	public bool IsPrivate => Coins.All(x => x.HdPubKey.AnonymitySet >= _privateCoinThreshold);

	public bool IsCustomFeeUsed { get; set; }

	public bool SubtractFee { get; set; }

	public void Reset()
	{
		Amount = Money.Zero;
		UserLabels = SmartLabel.Empty;
		MaximumPossibleFeeRate = null;
		ConfirmationTimeSpan = TimeSpan.Zero;
		Coins = Enumerable.Empty<SmartCoin>();
		ChangelessCoins = Enumerable.Empty<SmartCoin>();
		SubtractFee = default;

		if (!IsCustomFeeUsed)
		{
			FeeRate = FeeRate.Zero;
		}
	}

	private void OnFeeChanged()
	{
		ChangelessCoins = Enumerable.Empty<SmartCoin>();
	}

	private void OnCoinsChanged()
	{
		MaximumPossibleFeeRate = null;
	}

	public TransactionInfo Clone()
	{
		return new TransactionInfo(_privateCoinThreshold)
		{
			Amount = Amount,
			ChangelessCoins = ChangelessCoins,
			Coins = Coins,
			ConfirmationTimeSpan = ConfirmationTimeSpan,
			FeeRate = FeeRate,
			IsCustomFeeUsed = IsCustomFeeUsed,
			MaximumPossibleFeeRate = MaximumPossibleFeeRate,
			PayJoinClient = PayJoinClient,
			SubtractFee = SubtractFee,
			UserLabels = UserLabels
		};
	}
}

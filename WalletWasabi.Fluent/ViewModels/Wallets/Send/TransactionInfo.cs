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
	[AutoNotify] private FeeRate _feeRate = FeeRate.Zero;
	[AutoNotify] private IEnumerable<SmartCoin> _coins = Enumerable.Empty<SmartCoin>();

	public TransactionInfo(int anonScoreTarget)
	{
		PrivateCoinThreshold = anonScoreTarget;

		this.WhenAnyValue(x => x.FeeRate)
			.Subscribe(_ => OnFeeChanged());

		this.WhenAnyValue(x => x.Coins)
			.Subscribe(_ => OnCoinsChanged());
	}

	public int PrivateCoinThreshold { get; }

	/// <summary>
	/// In the case when InsufficientBalanceException happens, this amount should be
	/// taken into account when selecting pockets.
	/// </summary>
	public Money MinimumRequiredAmount { get; set; } = Money.Zero;

	public Money Amount { get; set; } = Money.Zero;

	public SmartLabel UserLabels { get; set; } = SmartLabel.Empty;

	public FeeRate? MaximumPossibleFeeRate { get; set; }

	public TimeSpan ConfirmationTimeSpan { get; set; }

	public IEnumerable<SmartCoin> ChangelessCoins { get; set; } = Enumerable.Empty<SmartCoin>();

	public IPayjoinClient? PayJoinClient { get; set; }

	public bool IsPayJoin => PayJoinClient is { };

	public bool IsOptimized => ChangelessCoins.Any();

	public bool IsPrivate => Coins.All(x => x.HdPubKey.AnonymitySet >= PrivateCoinThreshold);

	public bool IsCustomFeeUsed { get; set; }

	public bool SubtractFee { get; set; }

	public bool IsOtherPocketSelectionPossible { get; set; }
	
	public bool IsSelectedCoinModificationEnabled { get; set; } = true;

	public void Reset()
	{
		Amount = Money.Zero;
		MinimumRequiredAmount = Money.Zero;
		UserLabels = SmartLabel.Empty;
		MaximumPossibleFeeRate = null;
		ConfirmationTimeSpan = TimeSpan.Zero;
		Coins = Enumerable.Empty<SmartCoin>();
		ChangelessCoins = Enumerable.Empty<SmartCoin>();
		SubtractFee = default;
		IsOtherPocketSelectionPossible = default;

		if (!IsCustomFeeUsed)
		{
			FeeRate = FeeRate.Zero;
		}
	}

	private void OnFeeChanged()
	{
		ChangelessCoins = Enumerable.Empty<SmartCoin>();
		MinimumRequiredAmount = Money.Zero;
	}

	private void OnCoinsChanged()
	{
		MaximumPossibleFeeRate = null;
	}

	public TransactionInfo Clone()
	{
		return new TransactionInfo(PrivateCoinThreshold)
		{
			Amount = Amount,
			MinimumRequiredAmount = MinimumRequiredAmount,
			ChangelessCoins = ChangelessCoins,
			Coins = Coins,
			ConfirmationTimeSpan = ConfirmationTimeSpan,
			FeeRate = FeeRate,
			IsCustomFeeUsed = IsCustomFeeUsed,
			MaximumPossibleFeeRate = MaximumPossibleFeeRate,
			PayJoinClient = PayJoinClient,
			SubtractFee = SubtractFee,
			UserLabels = UserLabels,
			IsOtherPocketSelectionPossible = IsOtherPocketSelectionPossible
		};
	}
}

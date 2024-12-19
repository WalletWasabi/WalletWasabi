using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class TransactionInfo
{
	[AutoNotify] private FeeRate _feeRate = FeeRate.Zero;
	[AutoNotify] private IEnumerable<SmartCoin> _coins = Enumerable.Empty<SmartCoin>();

	public TransactionInfo(Destination destination, int anonScoreTarget)
	{
		Destination = destination;
		PrivateCoinThreshold = anonScoreTarget;

		this.WhenAnyValue(x => x.FeeRate)
			.Subscribe(_ => OnFeeChanged());

		this.WhenAnyValue(x => x.Coins)
			.Subscribe(_ => OnCoinsChanged());
	}

	public int PrivateCoinThreshold { get; }

	public Money Amount { get; init; } = Money.Zero;

	public Destination Destination { get; init; }

	public LabelsArray Recipient { get; set; } = LabelsArray.Empty;

	public FeeRate? MaximumPossibleFeeRate { get; set; }

	public TimeSpan ConfirmationTimeSpan { get; set; }

	public IEnumerable<SmartCoin> ChangelessCoins { get; set; } = Enumerable.Empty<SmartCoin>();

	public IPayjoinClient? PayJoinClient { get; set; }

	public bool IsPayJoin => PayJoinClient is { };

	public bool IsOptimized => ChangelessCoins.Any();

	public bool IsCustomFeeUsed { get; set; }

	public bool SubtractFee { get; init; }

	public bool IsOtherPocketSelectionPossible { get; set; }

	public bool IsSelectedCoinModificationEnabled { get; set; } = true;

	public bool IsFixedAmount { get; init; }

	private void OnFeeChanged()
	{
		ChangelessCoins = Enumerable.Empty<SmartCoin>();
	}

	private void OnCoinsChanged()
	{
		MaximumPossibleFeeRate = null;
		ChangelessCoins = Enumerable.Empty<SmartCoin>(); // Clear ChangelessCoins on pocket change, so we calculate the suggestions with the new pocket.
	}

	public TransactionInfo Clone()
	{
		return new TransactionInfo(Destination, PrivateCoinThreshold)
		{
			FeeRate = FeeRate,
			Coins = Coins,
			Amount = Amount,
			Destination = Destination,
			Recipient = Recipient,
			MaximumPossibleFeeRate = MaximumPossibleFeeRate,
			ConfirmationTimeSpan = ConfirmationTimeSpan,
			ChangelessCoins = ChangelessCoins,
			PayJoinClient = PayJoinClient,
			IsCustomFeeUsed = IsCustomFeeUsed,
			SubtractFee = SubtractFee,
			IsOtherPocketSelectionPossible = IsOtherPocketSelectionPossible,
			IsSelectedCoinModificationEnabled = IsSelectedCoinModificationEnabled,
			IsFixedAmount = IsFixedAmount
		};
	}
}

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

	public TransactionInfo(string displayAddress, PaymentIntent paymentIntent, int anonScoreTarget)
	{
		DisplayAddress = displayAddress;
		PaymentIntent = paymentIntent;
		PrivateCoinThreshold = anonScoreTarget;

		this.WhenAnyValue(x => x.FeeRate)
			.Subscribe(_ => OnFeeChanged());

		this.WhenAnyValue(x => x.Coins)
			.Subscribe(_ => OnCoinsChanged());
	}

	public int PrivateCoinThreshold { get; }

	public Money Amount => PaymentIntent.TotalAmount;

	public string DisplayAddress { get; init; }
	public PaymentIntent PaymentIntent { get; init; }
	public Destination Destination => PaymentIntent.Requests.First().Destination;
	public LabelsArray Recipient => PaymentIntent.Requests.Count() == 1 ?
		PaymentIntent.Requests.First().Labels :
		new LabelsArray(PaymentIntent.Requests.SelectMany(x => x.Labels));

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
		return new TransactionInfo(DisplayAddress, PaymentIntent, PrivateCoinThreshold)
		{
			FeeRate = FeeRate,
			Coins = Coins,
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

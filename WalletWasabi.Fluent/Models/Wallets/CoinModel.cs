using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class CoinModel : ReactiveObject
{
	private readonly IServices _services;
	private bool _subscribedToCoinChanges;

	[AutoNotify] private bool _isExcludedFromCoinJoin;
	[AutoNotify] private bool _isCoinJoinInProgress;
	[AutoNotify] private bool _isBanned;
	[AutoNotify] private string? _bannedUntilUtcToolTip;
	[AutoNotify] private string? _coinJoinIneligibilityToolTip;
	[AutoNotify] private string? _confirmedToolTip;
	[AutoNotify] private int _anonScore;
	[AutoNotify] private uint _confirmations;
	[AutoNotify] private bool _isConfirmed;

	public CoinModel(SmartCoin coin, Network network, int anonScoreTarget, IServices services)
	{
		_services = services;
		Coin = coin;
		PrivacyLevel = coin.GetPrivacyLevel(anonScoreTarget);
		Amount = coin.Amount;
		BtcAddress = coin.ScriptPubKey.GetDestinationAddress(network)?.ToString();

		Labels = coin.GetLabels(anonScoreTarget);
		Key = coin.Outpoint.GetHashCode();
		BannedUntilUtc = coin.BannedUntilUtc;
		ScriptType = ScriptType.FromEnum(coin.ScriptType);

		IsExcludedFromCoinJoin = coin.IsExcludedFromCoinJoin;
		IsConfirmed = coin.Confirmed;
		AnonScore = (int)coin.AnonymitySet;
		IsCoinJoinInProgress = coin.CoinJoinInProgress;
		IsBanned = coin.IsBanned;
		BannedUntilUtcToolTip = $"Can't participate in coinjoin until: {coin.BannedUntilUtc}";
		CoinJoinIneligibilityToolTip = GetCoinJoinIneligibilityToolTip(coin.CoinJoinIneligibility);

		var confirmations = coin.GetConfirmations(services.GetServerTipHeight());
		Confirmations = confirmations;
		ConfirmedToolTip = TextHelpers.GetConfirmationText(confirmations);
	}

	private SmartCoin Coin { get; }

	public Money Amount { get; }

	public string? BtcAddress { get; }

	public int Key { get; }

	public PrivacyLevel PrivacyLevel { get; }

	public LabelsArray Labels { get; }

	public ScriptType ScriptType { get; }

	public DateTimeOffset? BannedUntilUtc { get; }

	public bool IsPrivate => PrivacyLevel == PrivacyLevel.Private;

	public bool IsSemiPrivate => PrivacyLevel == PrivacyLevel.SemiPrivate;

	public bool IsNonPrivate => PrivacyLevel == PrivacyLevel.NonPrivate;

	/// <summary>Subscribes to property changes of underlying SmartCoin.</summary>
	/// <remarks>This method is not thread safe. Make sure it's not called concurrently.</remarks>
	public void SubscribeToCoinChanges(CompositeDisposable disposable)
	{
		if (_subscribedToCoinChanges)
		{
			return;
		}

		disposable.Add(Disposable.Create(() => _subscribedToCoinChanges = false));

		this.WhenAnyValue(c => c.Coin.IsExcludedFromCoinJoin).BindTo(this, x => x.IsExcludedFromCoinJoin).DisposeWith(disposable);
		this.WhenAnyValue(c => c.Coin.Confirmed).BindTo(this, x => x.IsConfirmed).DisposeWith(disposable);
		this.WhenAnyValue(c => c.Coin.AnonymitySet).Select(x => (int)x).BindTo(this, x => x.AnonScore).DisposeWith(disposable);
		this.WhenAnyValue(c => c.Coin.CoinJoinInProgress).BindTo(this, x => x.IsCoinJoinInProgress).DisposeWith(disposable);
		this.WhenAnyValue(c => c.Coin.BannedUntilUtc).Select(_ => Coin.IsBanned).BindTo(this, x => x.IsBanned).DisposeWith(disposable);
		this.WhenAnyValue(c => c.Coin.BannedUntilUtc).WhereNotNull().Subscribe(x => BannedUntilUtcToolTip = $"Can't participate in coinjoin until: {x:g}").DisposeWith(disposable);
		this.WhenAnyValue(c => c.Coin.CoinJoinIneligibility).Select(GetCoinJoinIneligibilityToolTip).BindTo(this, x => x.CoinJoinIneligibilityToolTip).DisposeWith(disposable);

		this.WhenAnyValue(c => c.Coin.Height).Select(_ => Coin.GetConfirmations(_services.GetServerTipHeight())).Subscribe(
			confirmations =>
			{
				Confirmations = confirmations;
				ConfirmedToolTip = TextHelpers.GetConfirmationText(confirmations);
			}).DisposeWith(disposable);

		_subscribedToCoinChanges = true;
	}

	public bool IsSameAddress(CoinModel anotherCoin) => anotherCoin.Coin.HdPubKey == Coin.HdPubKey;

	public bool IsSame(CoinModel anotherCoin) => anotherCoin.Coin.Outpoint == Coin.Outpoint;

	// TODO: Leaky abstraction. This shouldn't exist.
	public SmartCoin GetSmartCoin() => Coin;

	/// <returns>What to tell the user about a coin that the last coinjoin round did not accept, or <c>null</c> if there is nothing to tell.</returns>
	private string? GetCoinJoinIneligibilityToolTip(CoinJoinIneligibility? ineligibility) =>
		ineligibility switch
		{
			null => null,
			CoinJoinIneligibility.AmountTooLow tooLow => $"Can't participate in coinjoin: the amount is below the coordinator's minimum of {tooLow.MinAllowedAmount.ToFormattedString()} BTC",
			CoinJoinIneligibility.AmountTooHigh tooHigh => $"Can't participate in coinjoin: the amount is above the coordinator's maximum of {tooHigh.MaxAllowedAmount.ToFormattedString()} BTC",
			CoinJoinIneligibility.Uneconomical uneconomical => $"Can't participate in coinjoin: at the last round's mining fee rate of {uneconomical.MiningFeeRate.SatoshiPerByte:0.##} sat/vB it costs more to spend than it is worth",
			CoinJoinIneligibility.ScriptTypeNotAllowed => $"Can't participate in coinjoin: the coordinator doesn't accept {ScriptType.Name} coins",
			_ => "Can't participate in coinjoin"
		};
}

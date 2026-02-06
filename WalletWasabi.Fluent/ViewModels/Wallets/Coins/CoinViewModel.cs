using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public class CoinViewModel : CoinListItem
{
    public CoinViewModel(LabelsArray labels, CoinModel coin, bool canSelectWhenCoinjoining, bool ignorePrivacyMode)
	{
		Labels = labels;
		Coin = coin;
		BtcAddress = coin.BtcAddress;
		Amount = new Amount(coin.Amount);
		IsConfirmed = coin.IsConfirmed;
		IsBanned = coin.IsBanned;
		var confirmationCount = coin.Confirmations;
		ConfirmationStatus = $"{confirmationCount} confirmation{TextHelpers.AddSIfPlural(confirmationCount)}";
		BannedUntilUtcToolTip = coin.BannedUntilUtcToolTip;
		AnonymityScore = coin.AnonScore;
		BannedUntilUtc = coin.BannedUntilUtc;
		IsSelected = false;
		ScriptType = coin.ScriptType;
		IgnorePrivacyMode = ignorePrivacyMode;
		this.WhenAnyValue(x => x.Coin.IsExcludedFromCoinJoin).BindTo(this, x => x.IsExcludedFromCoinJoin).DisposeWith(_disposables);
		this.WhenAnyValue(x => x.Coin.IsCoinJoinInProgress).BindTo(this, x => x.IsCoinjoining).DisposeWith(_disposables);
		this.WhenAnyValue(x => x.CanBeSelected)
			.Where(b => !b)
			.Do(_ => IsSelected = false)
			.Subscribe();

        if (!canSelectWhenCoinjoining)
        {
            this.WhenAnyValue(x => x.Coin.IsCoinJoinInProgress, b => !b).BindTo(this, x => x.CanBeSelected).DisposeWith(_disposables);
        }
	}

	public CoinModel Coin { get; }
	public override string Key => Coin.Key.ToString();
}

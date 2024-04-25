using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public class CoinViewModel : CoinListItem, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public CoinViewModel(LabelsArray labels, ICoinModel coin, bool ignorePrivacyMode = false)
	{
		Labels = labels;
		Coin = coin;
		Amount = coin.Amount;
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
		this.WhenAnyValue(x => x.Coin.IsCoinJoinInProgress, b => !b).BindTo(this, x => x.CanBeSelected).DisposeWith(_disposables);
		this.WhenAnyValue(x => x.CanBeSelected)
			.Where(b => !b)
			.Do(_ => IsSelected = false)
			.Subscribe();
	}

	public ICoinModel Coin { get; }

	public void Dispose() => _disposables.Dispose();
}

using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

public partial class WalletCoinViewModel : ViewModelBase, IDisposable, ISelectable
{
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private string? _address;
	[AutoNotify] private Money _amount = Money.Zero;
	[AutoNotify] private int _anonymitySet;
	[AutoNotify] private string? _bannedUntilUtcToolTip;
	[AutoNotify] private bool _coinJoinInProgress;
	[AutoNotify] private bool _confirmed;
	[AutoNotify] private string? _confirmedToolTip;
	[AutoNotify] private bool _isBanned;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private SmartLabel _smartLabel = "";

	public WalletCoinViewModel(SmartCoin coin, Wallet wallet)
	{
		Coin = coin;
		Wallet = wallet;
		Amount = Coin.Amount;
		Address = Coin.TransactionId.ToString();

		Coin.WhenAnyValue(c => c.Confirmed).Subscribe(x => Confirmed = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.HdPubKey.Cluster.Labels).Subscribe(x => SmartLabel = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.HdPubKey.AnonymitySet).Subscribe(x => AnonymitySet = (int) x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.CoinJoinInProgress).Subscribe(x => CoinJoinInProgress = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.IsBanned).Subscribe(x => IsBanned = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.BannedUntilUtc).WhereNotNull().Subscribe(x => BannedUntilUtcToolTip = $"Banned until: {x:g}").DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.Height).Select(_ => Coin.GetConfirmations()).Subscribe(x => ConfirmedToolTip = $"{x} confirmation{TextHelpers.AddSIfPlural(x)}").DisposeWith(_disposables);
	}

	public SmartCoin Coin { get; }

	public Wallet Wallet { get; }

	public PrivacyLevelKey PrivacyLevelKey => PrivacyLevelKey.Get(SmartLabel, this.GetPrivacyLevel());

	public void Dispose()
	{
		_disposables.Dispose();
	}

	public static Comparison<WalletCoinViewModel?> SortAscending<T>(Func<WalletCoinViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}

			if (x is null)
			{
				return -1;
			}

			if (y is null)
			{
				return 1;
			}

			return Comparer<T>.Default.Compare(selector(x), selector(y));
		};
	}

	public static Comparison<WalletCoinViewModel?> SortDescending<T>(Func<WalletCoinViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}

			if (x is null)
			{
				return 1;
			}

			if (y is null)
			{
				return -1;
			}

			return Comparer<T>.Default.Compare(selector(y), selector(x));
		};
	}
}

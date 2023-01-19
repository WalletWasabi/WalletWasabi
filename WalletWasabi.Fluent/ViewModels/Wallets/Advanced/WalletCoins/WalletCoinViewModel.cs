using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

public partial class WalletCoinViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	[ObservableProperty] private Money _amount = Money.Zero;
	[ObservableProperty] private int _anonymitySet;
	[ObservableProperty] private SmartLabel _smartLabel = "";
	[ObservableProperty] private bool _confirmed;
	[ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ToggleSelectCommand))] private bool _coinJoinInProgress;
	[ObservableProperty] private bool _isSelected;
	[ObservableProperty] private bool _isBanned;
	[ObservableProperty] private string? _bannedUntilUtcToolTip;
	[ObservableProperty] private string? _confirmedToolTip;

	public WalletCoinViewModel(SmartCoin coin)
	{
		Coin = coin;
		Amount = Coin.Amount;

		ToggleSelectCommand = new RelayCommand(() => IsSelected = !IsSelected, canExecute: () => !CoinJoinInProgress);

		Coin.WhenAnyValue(c => c.Confirmed).Subscribe(x => Confirmed = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.HdPubKey.Cluster.Labels).Subscribe(x => SmartLabel = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.HdPubKey.AnonymitySet).Subscribe(x => AnonymitySet = (int)x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.CoinJoinInProgress).Subscribe(x => CoinJoinInProgress = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.IsBanned).Subscribe(x => IsBanned = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.BannedUntilUtc).WhereNotNull().Subscribe(x => BannedUntilUtcToolTip = $"Can't participate in coinjoin until: {x:g}").DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.Height).Select(_ => Coin.GetConfirmations()).Subscribe(x => ConfirmedToolTip = $"{x} confirmation{TextHelpers.AddSIfPlural(x)}").DisposeWith(_disposables);

		// Remove selection when coin participates in a coinjoin.
		this.WhenAnyValue(x => x.CoinJoinInProgress).Where(x => x).Subscribe(_ => IsSelected = false);
	}

	public IRelayCommand ToggleSelectCommand { get; }

	public SmartCoin Coin { get; }

	public static Comparison<WalletCoinViewModel?> SortAscending<T>(Func<WalletCoinViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}
			else if (x is null)
			{
				return -1;
			}
			else if (y is null)
			{
				return 1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(x), selector(y));
			}
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
			else if (x is null)
			{
				return 1;
			}
			else if (y is null)
			{
				return -1;
			}
			else
			{
				return Comparer<T>.Default.Compare(selector(y), selector(x));
			}
		};
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}

using NBitcoin;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

public partial class WalletCoinViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private bool _isExcludedFromCoinJoin;
	[AutoNotify] private Money _amount = Money.Zero;
	[AutoNotify] private int _anonymitySet;
	[AutoNotify] private SmartLabel _smartLabel = "";
	[AutoNotify] private bool _confirmed;
	[AutoNotify] private bool _coinJoinInProgress;
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isBanned;
	[AutoNotify] private string? _bannedUntilUtcToolTip;
	[AutoNotify] private string? _confirmedToolTip;

	public WalletCoinViewModel(SmartCoin coin)
	{
		Coin = coin;
		Amount = Coin.Amount;
		IsExcludedFromCoinJoin = coin.IsExcludedFromCoinJoin;

		Coin.WhenAnyValue(c => c.IsExcludedFromCoinJoin).Subscribe(x => IsExcludedFromCoinJoin = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.Confirmed).Subscribe(x => Confirmed = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.HdPubKey.Cluster.Labels).Subscribe(x => SmartLabel = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.HdPubKey.AnonymitySet).Subscribe(x => AnonymitySet = (int)x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.CoinJoinInProgress).Subscribe(x => CoinJoinInProgress = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.IsBanned).Subscribe(x => IsBanned = x).DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.BannedUntilUtc).WhereNotNull().Subscribe(x => BannedUntilUtcToolTip = $"Can't participate in coinjoin until: {x:g}").DisposeWith(_disposables);
		Coin.WhenAnyValue(c => c.Height).Select(_ => Coin.GetConfirmations()).Subscribe(x => ConfirmedToolTip = $"{x} confirmation{TextHelpers.AddSIfPlural(x)}").DisposeWith(_disposables);

		// Temporarily enable the selection no matter what.
		// Should be again restricted once https://github.com/zkSNACKs/WalletWasabi/issues/9972 is implemented.
		// this.WhenAnyValue(x => x.CoinJoinInProgress).Where(x => x).Subscribe(_ => IsSelected = false); // Remove selection when coin participates in a coinjoin.
		ToggleSelectCommand = ReactiveCommand.Create(() => IsSelected = !IsSelected/*, canExecute: this.WhenAnyValue(x => x.CoinJoinInProgress).Select(x => !x)*/);
	}

	public ICommand ToggleSelectCommand { get; }

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

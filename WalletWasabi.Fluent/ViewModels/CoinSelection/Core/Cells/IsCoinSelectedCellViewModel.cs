using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public partial class IsCoinSelectedCellViewModel : ViewModelBase, ISelectable
{
	private readonly CompositeDisposable _disposable = new();
	[AutoNotify] private bool _isSelected;
	[AutoNotify] private bool _isEnabled;

	public IsCoinSelectedCellViewModel(WalletCoinViewModel coin)
	{
		Coin = coin;

		this.WhenAnyValue(x => x.Coin.IsSelected, x => x.Coin.CoinJoinInProgress, (sel, cj) => sel && !cj)
			.Do(isSelected => IsSelected = isSelected)
			.Subscribe();

		this.WhenAnyValue(x => x.Coin.CoinJoinInProgress)
			.Do(isCoinJoining => IsEnabled = !isCoinJoining)
			.Subscribe();

		this.WhenAnyValue(model => model.IsSelected)
			.Do(b => coin.IsSelected = b)
			.Subscribe();
	}

	public WalletCoinViewModel Coin { get; }

	public void Dispose()
	{
		_disposable.Dispose();
	}
}

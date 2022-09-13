using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public partial class CoinSelectionCellViewModel : ViewModelBase, ISelectable
{
	[AutoNotify] private bool _isEnabled;
	[AutoNotify] private bool _isSelected;

	public CoinSelectionCellViewModel(WalletCoinViewModel coin)
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
}

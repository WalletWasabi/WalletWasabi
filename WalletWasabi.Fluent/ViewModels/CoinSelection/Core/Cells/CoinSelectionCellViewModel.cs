using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Controls;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;

public partial class CoinSelectionCellViewModel : ViewModelBase, ISelectable
{
	[AutoNotify] private bool _isEnabled;
	[AutoNotify] private bool _isSelected;

	public CoinSelectionCellViewModel(SelectableCoin coin)
	{
		Coin = coin;

		this.WhenAnyValue(x => x.Coin.IsSelected)
			.Do(b => IsSelected = b)
			.Subscribe();

		this.WhenAnyValue(x => x.Coin.IsCoinjoining)
			.Do(isCoinJoining => IsEnabled = !isCoinJoining)
			.Subscribe();

		this.WhenAnyValue(model => model.IsSelected)
			.Do(b => coin.IsSelected = b)
			.Subscribe();
	}

	public SelectableCoin Coin { get; }
}

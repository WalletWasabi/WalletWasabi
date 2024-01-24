using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.TreeDataGrid;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

public class WalletCoinViewModel : Selectable<ICoinModel>
{
	public WalletCoinViewModel(ICoinModel model, Action<ICoinModel>? onSelected = null, IObservable<bool>? canSelect = null) : base(model, onSelected, canSelect)
	{
	}
}

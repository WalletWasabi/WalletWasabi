using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.Design;

internal class WalletDashboardViewModelDesign : IWalletDashboardViewModel
{
	public ICollection<ViewModelBase> Children { get; set; } = new List<ViewModelBase>();
}
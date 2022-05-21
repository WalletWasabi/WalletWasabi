using System.Collections.Generic;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles.Design;

public interface IWalletDashboardViewModel
{
	ICollection<ViewModelBase> Children { get; set; }
}
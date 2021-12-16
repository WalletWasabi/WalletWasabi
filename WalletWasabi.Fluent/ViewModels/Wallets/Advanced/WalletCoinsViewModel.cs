using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced
{
	[NavigationMetaData(Title = "Wallet Coins")]
	public partial class WalletCoinsViewModel : RoutableViewModel
	{
		public WalletCoinsViewModel(WalletViewModelBase walletViewModelBase)
		{
			SetupCancel(false, true, true);
			NextCommand = CancelCommand;
		}
	}
}

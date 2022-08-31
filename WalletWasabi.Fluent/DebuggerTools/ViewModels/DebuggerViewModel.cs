using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebuggerViewModel : ViewModelBase
{
	[AutoNotify] private DebugWalletViewModel? _selectedWallet;

	public DebuggerViewModel()
	{
		Wallets = Services.WalletManager.GetWallets().Select(x => new DebugWalletViewModel(x)).ToList();

		SelectedWallet = Wallets.FirstOrDefault();
	}

	public List<DebugWalletViewModel> Wallets { get; }
}

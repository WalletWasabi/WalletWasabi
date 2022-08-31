using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

internal partial class DebuggerViewModel : ViewModelBase
{
	private readonly MainViewModel _mainViewModel;

	public DebuggerViewModel(MainViewModel mainViewModel)
	{
		_mainViewModel = mainViewModel;

		Wallets = Services.WalletManager.GetWallets();
	}

	public IEnumerable<Wallet> Wallets { get; }
}

using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets
{
	public class ConnectHardwareWalletViewModel : CategoryViewModel
	{
		public ConnectHardwareWalletViewModel() : base("Hardware Wallets")
		{
			//ConnectCommand = ReactiveCommand.CreateFromTask(LoadWalletAsync, this.WhenAnyValue(x => x.CanLoadWallet));
		}

		public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
	}
}

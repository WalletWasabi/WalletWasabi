using AvalonStudio.Extensibility;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Gui.Controls.WalletExplorer;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Tabs.WalletManager.LoadWallets;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets
{
	internal class ConnectHardwareWalletViewModel : LoadWalletViewModel
	{
		internal ConnectHardwareWalletViewModel(WalletManagerViewModel owner) : base(owner, LoadWalletType.Hardware)
		{
		}
	}
}

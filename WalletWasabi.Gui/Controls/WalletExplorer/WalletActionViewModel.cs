using System;
using System.Collections.Generic;
using System.Text;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class WalletActionViewModel : DocumentTabViewModel
	{
		public WalletViewModel Wallet { get; }

		public WalletActionViewModel(string title, WalletViewModel walletViewModel)
			: base(title)
		{
			Wallet = walletViewModel;
			DoItCommand = ReactiveCommand.Create(DisplayActionTab);
		}

		public ReactiveCommand DoItCommand { get; }

		public void DisplayActionTab()
		{
			if (Title == "Send")
			{
				IoC.Get<IShell>().AddOrSelectDocument<SendActionViewModel>(this);
			}
			else if (Title == "Receive")
			{
				IoC.Get<IShell>().AddOrSelectDocument<ReceiveActionViewModel>(this);
			}
			else if (Title == "CoinJoin")
			{
				IoC.Get<IShell>().AddOrSelectDocument<CoinJoinActionViewModel>(this);
			}
			else if (Title == "History")
			{
				IoC.Get<IShell>().AddOrSelectDocument<HistoryActionViewModel>(this);
			}
		}
	}
}

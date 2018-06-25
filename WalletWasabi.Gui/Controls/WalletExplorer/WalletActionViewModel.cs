using AvalonStudio.Controls;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

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
			IoC.Get<IShell>().AddOrSelectDocument(this);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabViewModel : WalletActionViewModel
	{
		public HistoryTabViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel) { }
	}
}

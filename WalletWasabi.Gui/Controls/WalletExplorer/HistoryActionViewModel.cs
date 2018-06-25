using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryActionViewModel : WalletActionViewModel
	{
		public HistoryActionViewModel(WalletViewModel walletViewModel)
			: base("History", walletViewModel) { }
	}
}

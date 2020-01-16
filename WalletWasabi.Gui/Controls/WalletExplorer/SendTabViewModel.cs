using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : SendTabBaseViewModel
	{
		public SendTabViewModel(WalletViewModel walletViewModel, bool isTransactionBuilder = false) : base(walletViewModel, isTransactionBuilder)
		{
		}
	}
}

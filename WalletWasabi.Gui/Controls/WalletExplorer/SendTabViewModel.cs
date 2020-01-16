using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class SendTabViewModel : SendTabBaseViewModel
	{
		public override string DoButtonText => "Send Transaction";
		public override string DoingButtonText => "Sending Transaction...";

		public SendTabViewModel(WalletViewModel walletViewModel, bool isTransactionBuilder = false) : base(walletViewModel, "Send")
		{
		}
	}
}

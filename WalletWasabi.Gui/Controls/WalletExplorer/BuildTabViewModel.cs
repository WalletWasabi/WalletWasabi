using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class BuildTabViewModel : SendTabBaseViewModel
	{
		public override string DoButtonText => "Build Transaction";
		public override string DoingButtonText => "Building Transaction...";

		public BuildTabViewModel(WalletViewModel walletViewModel) : base(walletViewModel, "Build Transaction")
		{
		}
	}
}

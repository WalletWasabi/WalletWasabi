using System;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class PrivacyPolicyViewModel : TextResourceViewModelBase
	{
		public PrivacyPolicyViewModel(Global global) : base(global, "Privacy Policy", new Uri("resm:WalletWasabi.Gui.Assets.PrivacyPolicy.txt"))
		{
		}
	}
}

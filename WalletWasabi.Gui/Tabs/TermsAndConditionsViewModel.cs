using System;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class TermsAndConditionsViewModel : TextResourceViewModelBase
	{
		public TermsAndConditionsViewModel(Global global) : base(global, "Terms and Conditions", new Uri("resm:WalletWasabi.Gui.Assets.TermsAndConditions.txt"))
		{
		}
	}
}

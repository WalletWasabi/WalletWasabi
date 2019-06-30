using System;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalIssuesViewModel : TextResourceViewModelBase
	{
		public LegalIssuesViewModel(Global global) : base(global, "Legal Issues", new Uri("resm:WalletWasabi.Gui.Assets.LegalIssues.txt"))
		{
		}
	}
}

using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class LegalIssuesViewModel : DocumentTabViewModel
	{
		public LegalIssuesViewModel() : base("Legal Issues")
		{
			LegalIssues = @"";
		}

		public string LegalIssues { get; }
	}
}

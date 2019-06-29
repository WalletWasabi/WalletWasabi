using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class LegalIssuesViewModel : WasabiDocumentTabViewModel
	{
		public LegalIssuesViewModel(Global global) : base(global, "Legal Issues")
		{
			LegalIssues = @"";

			LegalIssues += new string('\n', 5);
		}

		public string LegalIssues { get; }
	}
}

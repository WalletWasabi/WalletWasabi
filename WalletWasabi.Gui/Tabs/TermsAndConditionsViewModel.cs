using Avalonia.Diagnostics.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class TermsAndConditionsViewModel : WasabiDocumentTabViewModel
	{
		public TermsAndConditionsViewModel() : base("Terms and Conditions")
		{
			TermsAndConditions = @"";

			TermsAndConditions += new string('\n', 100);
		}

		public string TermsAndConditions { get; }
	}
}

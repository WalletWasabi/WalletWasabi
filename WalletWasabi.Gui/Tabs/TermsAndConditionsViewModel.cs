using Avalonia.Diagnostics.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class TermsAndConditionsViewModel : DocumentTabViewModel
	{
		public TermsAndConditionsViewModel() : base("Terms and Conditions")
		{
			TermsAndConditions = "These are the terms and conditions ****";
		}

		public string TermsAndConditions { get; }
	}
}

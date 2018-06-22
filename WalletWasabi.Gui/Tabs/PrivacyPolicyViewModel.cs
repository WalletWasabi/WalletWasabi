using Avalonia.Diagnostics.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class PrivacyPolicyViewModel : DocumentTabViewModel
	{
		public PrivacyPolicyViewModel() : base("Privacy Policy")
		{
			PrivacyPolicy = "This is the privacy policy *****";
		}

		public string PrivacyPolicy { get; }

	}
}

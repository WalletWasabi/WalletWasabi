using Avalonia.Diagnostics.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class PrivacyPolicyViewModel : WasabiDocumentTabViewModel
	{
		public PrivacyPolicyViewModel(Global global) : base(global, "Privacy Policy")
		{
			PrivacyPolicy = @"";

			PrivacyPolicy += new string('\n', 5);
		}

		public string PrivacyPolicy { get; }
	}
}

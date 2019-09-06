using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs.LegalDocs
{
	internal class PrivacyPolicyViewModel : TextResourceViewModelBase
	{
		public PrivacyPolicyViewModel(Global global) : base(global, "Privacy Policy", new Uri(Path.Combine(global.DataDir, "UpdateChecker", "PrivacyPolicy.txt")))
		{
		}
	}
}

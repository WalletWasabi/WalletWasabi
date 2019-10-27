using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
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

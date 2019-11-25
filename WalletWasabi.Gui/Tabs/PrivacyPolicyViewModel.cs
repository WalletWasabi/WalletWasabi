using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class PrivacyPolicyViewModel : TextResourceViewModelBase
	{
		public PrivacyPolicyViewModel() : base("Privacy Policy", new Uri("avares://WalletWasabi.Gui/Assets/PrivacyPolicy.txt"))
		{
		}
	}
}

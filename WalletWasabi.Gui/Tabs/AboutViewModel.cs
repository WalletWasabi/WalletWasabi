using System;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class AboutViewModel : DocumentTabViewModel
	{
		public AboutViewModel() : base("About")
		{
			Version = new Version(0, 7);
		}

		public Version Version { get; }

		public string VersionText => $"Wasabi Wallet v{Version.ToString()}";
	}
}

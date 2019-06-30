using System;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	internal class AboutViewModel : WasabiDocumentTabViewModel
	{
		public AboutViewModel(Global global) : base(global, "About")
		{
			Version = WalletWasabi.Helpers.Constants.ClientVersion;
		}

		public Version Version { get; }

		public string VersionText => $"v{Version.ToString()}";
	}
}

using Avalonia.Diagnostics.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using System.IO;
using ReactiveUI;
using System.Reactive;

namespace WalletWasabi.Gui.Tabs
{
	internal class AboutViewModel : WasabiDocumentTabViewModel
	{
		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		public AboutViewModel(Global global) : base(global, "About")
		{
			Version = WalletWasabi.Helpers.Constants.ClientVersion;

			OpenBrowserCommand = ReactiveCommand.Create<string>(x =>
			{
				try
				{
					IoHelpers.OpenBrowser(x);
				}
				catch (Exception ex)
				{
					Logging.Logger.LogError<AboutViewModel>(ex);
				}
			});
		}

		public Version Version { get; }

#if RELEASE
		public string VersionText => $"v{Version.ToString(3)}";
#else
		public string VersionText => $"v{Version.ToString(4)}";
#endif

		public string ClearnetLink => "https://wasabiwallet.io/";

		public string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";

		public string StatusPageLink => "https://stats.uptimerobot.com/W7q65in4y";

		public string CustomerSupportLink => "https://www.reddit.com/r/WasabiWallet/";

		public string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public string FAQLink => "https://docs.wasabiwallet.io/FAQ/";

		public string DocsLink => "https://docs.wasabiwallet.io/";
	}
}

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
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Tabs
{
	internal class AboutViewModel : WasabiDocumentTabViewModel
	{
		public ReactiveCommand<string, Unit> OpenBrowserCommand { get; }

		public AboutViewModel(Global global) : base(global, "About")
		{
			OpenBrowserCommand = ReactiveCommand.Create<string>(x => IoHelpers.OpenBrowser(x));

			OpenBrowserCommand.ThrownExceptions.Subscribe(ex => Logger.LogError(ex));
		}

		public Version ClientVersion => Constants.ClientVersion;
		public string BackendMajorVersion => Constants.BackendMajorVersion;
		public Version BitcoinCoreVersion => Constants.BitcoinCoreVersion;
		public Version HwiVersion => Constants.HwiVersion;

		public string ClearnetLink => "https://wasabiwallet.io/";

		public string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";

		public string StatusPageLink => "https://stats.uptimerobot.com/YQqGyUL8A7";

		public string CustomerSupportLink => "https://www.reddit.com/r/WasabiWallet/";

		public string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public string FAQLink => "https://docs.wasabiwallet.io/FAQ/";

		public string DocsLink => "https://docs.wasabiwallet.io/";
	}
}

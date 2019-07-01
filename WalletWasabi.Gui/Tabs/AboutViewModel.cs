using Avalonia.Diagnostics.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
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

		public string ClearnetLink => "https://wasabiwallet.io/";

		public string TorLink => "http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion";

		public string SourceCodeLink => "https://github.com/zkSNACKs/WalletWasabi/";

		public string CustomerSupportLink => "https://www.reddit.com/r/WasabiWallet/";

		public string BugReportLink => "https://github.com/zkSNACKs/WalletWasabi/issues/";

		public string FAQLink => "https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/FAQ.md";

		public void OnClearnetClicked()
		{
			OpenLink(ClearnetLink);
		}

		public void OnSourceCodeClicked()
		{
			OpenLink(SourceCodeLink);
		}

		public void OnCustomerSupportClicked()
		{
			OpenLink(CustomerSupportLink);
		}

		public void OnBugReportClicked()
		{
			OpenLink(BugReportLink);
		}

		public void OnFAQClicked()
		{
			OpenLink(FAQLink);
		}

		public void OpenLink(string url)
		{
			try
			{
				Process.Start(url);
			}
			catch
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start("xdg-open", url);
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", url);
				}
			}
		}
	}
}

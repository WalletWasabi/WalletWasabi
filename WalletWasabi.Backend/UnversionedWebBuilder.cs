using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Backend
{
	public static class UnversionedWebBuilder
	{
		public static string DebugRootFolder { get; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\wwwroot"));
		public static string PublishedRootFolder { get; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));

		public static string RootFolder { get; } = Directory.Exists(PublishedRootFolder) ? PublishedRootFolder : DebugRootFolder;

		public static string UnversionedFolder { get; } = Path.GetFullPath(Path.Combine(RootFolder, "unversioned"));

		public static string CreateFilePath(string fileName) => Path.Combine(UnversionedFolder, fileName);

		public static string HtmlStartLine { get; } = "<link href=\"../css/bootstrap.css\" rel=\"stylesheet\" type=\"text/css\" />\r\n<link href=\"../css/OpenSansCondensed300700.css\" rel=\"stylesheet\" type=\"text/css\" />\r\n";

		public static void CreateDownloadTextWithVersionHtml()
		{
			var filePath = CreateFilePath("download-text-with-version.html");
			var content = HtmlStartLine + $"<h1 class=\"text-center\">Download Wasabi Wallet {Constants.ClientVersion}</h1>";

			File.WriteAllText(filePath, content);
		}

		public static void CloneAndUpdateOnionIndexHtml()
		{
			var path = Path.Combine(RootFolder, "index.html");
			var onionPath = Path.Combine(RootFolder, "onion-index.html");

			var content = File.ReadAllText(path);

			content = content.Replace("coinjoins-table.html", "onion-coinjoins-table.html", StringComparison.Ordinal);
			content = content.Replace("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion", "https://wasabiwallet.io", StringComparison.Ordinal);
			content = content.Replace("images/tor-browser.png", "images/chrome-browser.png", StringComparison.Ordinal);

			File.WriteAllText(onionPath, content);
		}

		public static void UpdateCoinJoinsHtml(IEnumerable<string> coinJoins)
		{
			var filePath = CreateFilePath("coinjoins-table.html");
			var onionFilePath = CreateFilePath("onion-coinjoins-table.html");

			var content = HtmlStartLine + "<ul class=\"text-center\" style=\"list-style: none;\">";
			var onionContent = HtmlStartLine + "<ul class=\"text-center\" style=\"list-style: none;\">";
			var endContent = "</ul>";
			string blockstreamPath;
			string onionBlockstreamPath;
			if (Global.Instance.Config.Network == Network.TestNet)
			{
				blockstreamPath = "https://blockstream.info/testnet/tx/";
				onionBlockstreamPath = "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/testnet/tx/";
			}
			else
			{
				blockstreamPath = "https://blockstream.info/tx/";
				onionBlockstreamPath = "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/tx/";
			}

			var coinJoinsList = coinJoins.ToList();
			for (int i = 0; i < coinJoinsList.Count; i++)
			{
				string cjHash = coinJoinsList[i];

				if (i % 2 == 0)
				{
					content += $"<li style=\"background:#e6e6e6; margin:5px;\"><a href=\"{blockstreamPath}{cjHash}\" target=\"_blank\">{cjHash}</a></li>";
					onionContent += $"<li style=\"background:#e6e6e6; margin:5px;\"><a href=\"{onionBlockstreamPath}{cjHash}\" target=\"_blank\">{cjHash}</a></li>";
				}
				else
				{
					content += $"<li><a href=\"{blockstreamPath}{cjHash}\" target=\"_blank\">{cjHash}</a></li>";
					onionContent += $"<li><a href=\"{onionBlockstreamPath}{cjHash}\" target=\"_blank\">{cjHash}</a></li>";
				}
			}

			content += endContent;
			onionContent += endContent;
			File.WriteAllText(filePath, content);
			File.WriteAllText(onionFilePath, onionContent);
		}
	}
}

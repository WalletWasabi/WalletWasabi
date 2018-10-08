using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Backend
{
	public static class UnversionedWebBuilder
	{
		public static string UnversionedFolder { get; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\wwwroot\\unversioned"));

		public static string CreateFilePath(string fileName) => Path.Combine(UnversionedFolder, fileName);

		public static string HtmlStartLine { get; } = "<link href=\"../css/bootstrap.css\" rel=\"stylesheet\" type=\"text/css\" />\r\n<link href=\"../css/OpenSansCondensed300700.css\" rel=\"stylesheet\" type=\"text/css\" />\r\n";

		public static void CreateDownloadTextWithVersionHtml()
		{
			var filePath = CreateFilePath("download-text-with-version.html");
			var content = HtmlStartLine + $"<h1 class=\"text-center\">Download Wasabi Wallet {Helpers.Constants.ClientVersion.ToString()}</h1>";

			File.WriteAllText(filePath, content);
		}

		public static void UpdateMixedTextHtml(Money amount)
		{
			var filePath = CreateFilePath("mixed-text.html");

			var moneyString = amount.ToString(false, false);
			int index = moneyString.IndexOf(".");
			if (index > 0)
			{
				moneyString = moneyString.Substring(0, index);
			}
			var content = HtmlStartLine + $"<h2 class=\"text-center\">Wasabi made over <span style=\"padding-left:5px; padding-right:5px; display:inline-block\" class=\"inline border border-dark rounded bg-muted\">{moneyString} BTC</span> fungible since August 1, 2018.</h2>";

			File.WriteAllText(filePath, content);
		}
	}
}

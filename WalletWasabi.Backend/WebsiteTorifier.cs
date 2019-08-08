using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WalletWasabi.Backend
{
	public class WebsiteTorifier
	{
		public string RootFolder { get; }
		public string UnversionedFolder { get; }

		public WebsiteTorifier(string rootFolder)
		{
			RootFolder = rootFolder;
			UnversionedFolder = Path.GetFullPath(Path.Combine(RootFolder, "unversioned"));
		}

		public void CloneAndUpdateOnionIndexHtml()
		{
			var path = Path.Combine(RootFolder, "index.html");
			var onionPath = Path.Combine(RootFolder, "onion-index.html");

			var content = File.ReadAllText(path);

			content = content.Replace("http://wasabiukrxmkdgve5kynjztuovbg43uxcbcxn6y2okcrsg7gb6jdmbad.onion", "https://wasabiwallet.io", StringComparison.Ordinal);

			File.WriteAllText(onionPath, content);
		}
	}
}

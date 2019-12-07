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
			content = content.Replace("https://blockstream.info/tx/e4a789d16a24a6643dfee06e018ad27648b896daae6a3577ae0f4eddcc4d9174", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/tx/e4a789d16a24a6643dfee06e018ad27648b896daae6a3577ae0f4eddcc4d9174", StringComparison.Ordinal);
			content = content.Replace("https://blockstream.info/tx/c69aed505ca50473e2883130221915689c1474be3c66bcf7ac7dc0e26246afc8", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/tx/c69aed505ca50473e2883130221915689c1474be3c66bcf7ac7dc0e26246afc8", StringComparison.Ordinal);
			content = content.Replace("https://blockstream.info/tx/ef329b3ed8e790f10f0b522346f1b3d9f1c9d45dfa5b918e92d6f0a25d91c7ce", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/tx/ef329b3ed8e790f10f0b522346f1b3d9f1c9d45dfa5b918e92d6f0a25d91c7ce", StringComparison.Ordinal);
			content = content.Replace("https://blockstream.info/tx/f82206145413db5c1272d5609c88581c414815e36e400aee6410e0de9a2d46b5", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/tx/f82206145413db5c1272d5609c88581c414815e36e400aee6410e0de9a2d46b5", StringComparison.Ordinal);
			content = content.Replace("https://blockstream.info/tx/a7157780b7c696ab24767113d9d34cdbc0eba5c394c89aec4ed1a9feb326bea5", "http://explorerzydxu5ecjrkwceayqybizmpjjznk5izmitf2modhcusuqlid.onion/tx/a7157780b7c696ab24767113d9d34cdbc0eba5c394c89aec4ed1a9feb326bea5", StringComparison.Ordinal);

			File.WriteAllText(onionPath, content);
		}
	}
}

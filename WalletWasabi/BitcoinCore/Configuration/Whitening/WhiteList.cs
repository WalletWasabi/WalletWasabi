using NBitcoin;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace WalletWasabi.BitcoinCore.Configuration.Whitening
{
	public class WhiteList : WhiteEntry
	{
		public static bool TryParse(string value, Network network, out WhiteList white)
			=> TryParse<WhiteList>(value, network, out white);
	}
}

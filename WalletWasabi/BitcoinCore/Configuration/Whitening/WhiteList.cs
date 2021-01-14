using NBitcoin;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.BitcoinCore.Configuration.Whitening
{
	public class WhiteList : WhiteEntry
	{
		public static bool TryParse(string value, Network network, [NotNullWhen(true)] out WhiteList? white)
			=> TryParse<WhiteList>(value, network, out white);
	}
}

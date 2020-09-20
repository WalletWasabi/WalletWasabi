using NBitcoin;

namespace WalletWasabi.BitcoinCore.Configuration.Whitening
{
	public class WhiteList : WhiteEntry
	{
		public static bool TryParse(string value, Network network, out WhiteList white)
			=> TryParse<WhiteList>(value, network, out white);
	}
}

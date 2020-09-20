using NBitcoin;

namespace WalletWasabi.BitcoinCore.Configuration.Whitening
{
	public class WhiteBind : WhiteEntry
	{
		public static bool TryParse(string value, Network network, out WhiteBind white)
			=> TryParse<WhiteBind>(value, network, out white);
	}
}

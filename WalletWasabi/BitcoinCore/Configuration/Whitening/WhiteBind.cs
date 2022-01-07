using NBitcoin;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.BitcoinCore.Configuration.Whitening;

public class WhiteBind : WhiteEntry
{
	public static bool TryParse(string value, Network network, [NotNullWhen(true)] out WhiteBind? white)
		=> TryParse<WhiteBind>(value, network, out white);
}

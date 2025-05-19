using System.Diagnostics.CodeAnalysis;
using NBitcoin;

namespace WalletWasabi.Tests.BitcoinCore.Configuration.Whitening;

public class WhiteBind : WhiteEntry
{
	public static bool TryParse(string value, [NotNullWhen(true)] out WhiteBind? white)
		=> TryParse<WhiteBind>(value, out white);
}

using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class MainNetBitcoinP2pEndPointConverterNg : EndPointJsonConverterNg
{
	public MainNetBitcoinP2pEndPointConverterNg()
		: base(Constants.DefaultMainNetBitcoinP2pPort)
	{
	}
}

using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class TestNetBitcoinP2pEndPointConverterNg : EndPointJsonConverterNg
{
	public TestNetBitcoinP2pEndPointConverterNg()
		: base(Constants.DefaultTestNetBitcoinP2pPort)
	{
	}
}

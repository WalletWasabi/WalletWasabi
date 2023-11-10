using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class RegTestBitcoinP2pEndPointConverterNg : EndPointJsonConverterNg
{
	public RegTestBitcoinP2pEndPointConverterNg()
		: base(Constants.DefaultRegTestBitcoinCoreRpcPort)
	{
	}
}

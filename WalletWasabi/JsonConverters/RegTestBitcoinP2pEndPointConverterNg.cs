using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.JsonConverters;

public class RegTestBitcoinP2pEndPointConverterNg : EndPointJsonConverterNg
{
	public RegTestBitcoinP2pEndPointConverterNg()
		: base(Constants.DefaultRegTestBitcoinCoreRpcPort)
	{
	}
}

using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot);
}

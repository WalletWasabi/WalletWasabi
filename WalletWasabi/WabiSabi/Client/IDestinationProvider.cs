using NBitcoin;
using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	IEnumerable<IDestination> GetNextDestinations(int count);
}

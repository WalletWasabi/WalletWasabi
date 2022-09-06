using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	IEnumerable<IDestination> GetNextDestinations(int count);
}

public static class DestinationProviderExtensions
{
	public static Script Peek(this IDestinationProvider me) =>
		me.GetNextDestinations(1).First().ScriptPubKey;
}
using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	IEnumerable<ScriptType> SupportedScriptTypes { get; }

	IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot);
}

public static class DestinationProviderExtensions
{
	public static Script Peek(this IDestinationProvider me, bool preferTaproot) =>
		me.GetNextDestinations(1, preferTaproot).First().ScriptPubKey;
}

using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot);
	IEnumerable<ScriptType> SupportedScriptTypes { get; }
}

public static class DestinationProviderExtensions
{
	public static Script Peek(this IDestinationProvider me, bool preferTaproot) =>
		me.GetNextDestinations(1, preferTaproot).First().ScriptPubKey;
}

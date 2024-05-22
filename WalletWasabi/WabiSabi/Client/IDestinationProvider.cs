using NBitcoin;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	IEnumerable<ScriptType> SupportedScriptTypes { get; }

	IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot);

	public void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts);
}

using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	Task<IEnumerable<IDestination>> GetNextDestinationsAsync(int count, bool preferTaproot);
	
	Task<IEnumerable<PendingPayment>> GetPendingPaymentsAsync(UtxoSelectionParameters roundParameters);
}

public static class DestinationProviderExtensions
{
	public static Script Peek(this IDestinationProvider me, bool preferTaproot) =>
		me.GetNextDestinationsAsync(1, preferTaproot).Result.First().ScriptPubKey;
}

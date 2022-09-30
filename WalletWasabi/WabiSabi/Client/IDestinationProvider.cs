using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	Task<IEnumerable<IDestination>> GetNextDestinations(int count);
	
	Task<IEnumerable<PendingPayment>> GetPendingPayments(RoundParameters roundParameters,ImmutableArray<AliceClient> registeredAliceClients);
}

public class PendingPayment
{
	public IDestination Destination { get; set; }
	public Money Value { get; set; }
	public Action PaymentStarted { get; set; }
	public Action PaymentFailed { get; set; }
}

public static class DestinationProviderExtensions
{
	public static Script Peek(this IDestinationProvider me, bool preferTaproot) =>
		me.GetNextDestinations(1, preferTaproot).First().ScriptPubKey;
}
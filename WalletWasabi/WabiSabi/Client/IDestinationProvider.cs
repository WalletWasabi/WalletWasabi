using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	Task<IEnumerable<IDestination>> GetNextDestinationsAsync(int count, bool preferTaproot, bool mixedOutputs);
	
	Task<IEnumerable<PendingPayment>> GetPendingPaymentsAsync(UtxoSelectionParameters roundParameters);
}

public class PendingPayment
{
	public IDestination Destination { get; set; }
	public Money Value { get; set; }
	public Func<Task<bool>> PaymentStarted { get; set; }
	public Action PaymentFailed { get; set; }
	public Action<(uint256 roundId, uint256 transactionId, int outputIndex)> PaymentSucceeded { get; set; }
	public string Identifier { get; set; }

	public TxOut ToTxOut()
	{
		return new TxOut(Value, Destination);
	}
}

public static class DestinationProviderExtensions
{
	public static Script Peek(this IDestinationProvider me, bool preferTaproot) =>
		me.GetNextDestinationsAsync(1, preferTaproot, true).Result.First().ScriptPubKey;
}
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Client;

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

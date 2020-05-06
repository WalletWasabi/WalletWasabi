using System;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.WebClients.PayJoin
{
	public interface IPayjoinClient
	{
		Uri PaymentUrl { get; }
		Task<PSBT> TryNegotiatePayjoin(Func<PSBT, Task<PSBT>> sign, PSBT psbt, KeyManager keyManager);
	}
}
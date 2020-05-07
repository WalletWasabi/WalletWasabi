using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.WebClients.PayJoin
{
	public interface IPayjoinClient
	{
		Uri PaymentUrl { get; }
		Task<PSBT> TryNegotiatePayjoin(Func<PSBT, CancellationToken, Task<(PSBT PSBT, bool Signed)>> sign, PSBT psbt,
			KeyManager keyManager, CancellationToken cancellationToken);
	}
}
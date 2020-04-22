using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.WebClients.PayJoin
{
	public class NullPayjoinClient : IPayjoinClient
	{
		public Uri PaymentUrl => new Uri("https://");

		public Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey accountKey, RootedKeyPath rootedKeyPath, CancellationToken cancellationToken)
		{
			return Task.FromResult(originalTx);
		}
	}
}

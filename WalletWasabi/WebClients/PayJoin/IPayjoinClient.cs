using System;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.WebClients.PayJoin
{
	public interface IPayjoinClient
	{
		Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey accountKey, RootedKeyPath rootedKeyPath, CancellationToken cancellationToken);
	}
}
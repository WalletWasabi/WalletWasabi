using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public interface ITxPropagationVerifierApiClient
{
	public Task<bool> IsTxAcceptedByNode(uint256 txid, CancellationToken cancel);
}

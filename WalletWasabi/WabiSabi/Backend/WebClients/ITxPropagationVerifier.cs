using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public interface ITxPropagationVerifier
{
	public Task<bool> IsTxAcceptedByNode(uint256 txid, CancellationToken cancel);
}

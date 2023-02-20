using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Backend.WebClients;

public interface ITxPropagationVerifier
{
	public Task<bool?> GetTransactionStatusAsync(uint256 txid, CancellationToken cancel);
}

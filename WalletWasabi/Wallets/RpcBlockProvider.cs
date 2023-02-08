using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

public class RpcBlockProvider : IBlockProvider
{
	public RpcBlockProvider(CoreNode? coreNode)
	{
		CoreNode = coreNode;
	}
	
	private CoreNode? CoreNode { get; }

	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
    {
	    if (CoreNode?.RpcClient is null)
	    {
		    return null;
	    }

	    try
	    {
		    return await CoreNode!.RpcClient.GetBlockAsync(hash, cancellationToken).ConfigureAwait(false);
	    }
	    catch (Exception ex)
	    {
		    Logger.LogDebug(ex);
		    return null;
	    }
    }
}
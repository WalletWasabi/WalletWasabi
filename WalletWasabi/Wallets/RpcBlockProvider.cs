using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

public class RpcBlockProvider : IBlockProvider
{
	public RpcBlockProvider(IRPCClient rpcClient)
	{
		_rpcClient = rpcClient;
	}

	private readonly IRPCClient _rpcClient;

	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		try
		{
			return await _rpcClient.GetBlockAsync(hash, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogDebug(ex);
			return null;
		}
	}
}

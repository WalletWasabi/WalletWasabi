using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using System.Collections.Immutable;

namespace WalletWasabi.WabiSabi.Backend;

public class CoinJoinMempoolManager : PeriodicRunner
{
	public CoinJoinMempoolManager(ICoinJoinIdStore coinJoinIdStore, IRPCClient rpc) : base(TimeSpan.FromMinutes(1))
	{
		CoinJoinIdStore = coinJoinIdStore;
		RpcClient = rpc;
	}

	private ICoinJoinIdStore CoinJoinIdStore { get; }
	private IRPCClient RpcClient { get; }
	public ImmutableArray<uint256> CoinJoinIds { get; private set; } = ImmutableArray.Create<uint256>();

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		uint256[] mempoolHashes = await RpcClient.GetRawMempoolAsync(cancel).ConfigureAwait(false);
		var coinJoinsInMempool = mempoolHashes.Where(CoinJoinIdStore.Contains);
		CoinJoinIds = coinJoinsInMempool.ToImmutableArray();
	}
}

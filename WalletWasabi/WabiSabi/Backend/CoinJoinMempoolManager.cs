using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using System.Collections.Immutable;
using WalletWasabi.BitcoinCore.Mempool;

namespace WalletWasabi.WabiSabi.Backend;

public class CoinJoinMempoolManager : IDisposable
{
	private bool _disposedValue;

	public CoinJoinMempoolManager(ICoinJoinIdStore coinJoinIdStore)
	{
		CoinJoinIdStore = coinJoinIdStore;
	}

	private ICoinJoinIdStore CoinJoinIdStore { get; }
	private MempoolMirror Mempool { get; set; }
	public ImmutableArray<uint256> CoinJoinIds { get; private set; } = ImmutableArray.Create<uint256>();

	public void RegisterMempoolProvider(MempoolMirror mempool)
	{
		Mempool = mempool;
		Mempool.Tick += Mempool_Tick;
	}

	private void Mempool_Tick(object? sender, TimeSpan e)
	{
		var mempoolHashes = Mempool.GetMempoolHashes();
		var coinJoinsInMempool = mempoolHashes.Where(CoinJoinIdStore.Contains);
		CoinJoinIds = coinJoinsInMempool.ToImmutableArray();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				Mempool.Tick -= Mempool_Tick;
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

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

	public CoinJoinMempoolManager(ICoinJoinIdStore coinJoinIdStore, MempoolMirror mempoolMirror)
	{
		CoinJoinIdStore = coinJoinIdStore;
		MempoolMirror = mempoolMirror;
		MempoolMirror.Tick += Mempool_Tick;
	}

	private ICoinJoinIdStore CoinJoinIdStore { get; }
	private MempoolMirror MempoolMirror { get; }
	public ImmutableArray<uint256> CoinJoinIds { get; private set; } = ImmutableArray.Create<uint256>();

	private void Mempool_Tick(object? sender, TimeSpan e)
	{
		var mempoolHashes = MempoolMirror.GetMempoolHashes();
		var coinJoinsInMempool = mempoolHashes.Where(CoinJoinIdStore.Contains);
		CoinJoinIds = coinJoinsInMempool.ToImmutableArray();
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				MempoolMirror.Tick -= Mempool_Tick;
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

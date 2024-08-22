using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;

namespace WalletWasabi.BitcoinCore.Mempool;

public class MempoolMirror : PeriodicRunner
{
	/// <param name="period">How often to mirror the mempool.</param>
	public MempoolMirror( IRPCClient rpc, P2pNode node, TimeSpan? period = null) : base(period ?? TimeSpan.FromSeconds(21))
	{
		Rpc = rpc;
		Node = node;
	}

	private IRPCClient Rpc { get; }
	private P2pNode Node { get; }
	private Mempool Mempool { get; set; } = new();
	private object MempoolLock { get; } = new();

	public override Task StartAsync(CancellationToken cancellationToken)
	{
		Node.MempoolService.TransactionReceived += MempoolService_TransactionReceived;
		return base.StartAsync(cancellationToken);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var sw = Stopwatch.StartNew();
		var changes = await MirrorMempoolAsync(stoppingToken).ConfigureAwait(false);
		Logger.LogInfo($"{changes.Added} transactions were copied from the full node to the in-memory mempool and {changes.Removed} transactions were removed from the in-memory mempool within {sw.Elapsed.TotalSeconds} seconds.");

		await base.ExecuteAsync(stoppingToken).ConfigureAwait(false);
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		await MirrorMempoolAsync(cancel).ConfigureAwait(false);
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		Node.MempoolService.TransactionReceived -= MempoolService_TransactionReceived;
		return base.StopAsync(cancellationToken);
	}

	private void MempoolService_TransactionReceived(object? sender, SmartTransaction stx)
	{
		AddTransactions(stx.Transaction);
	}

	internal int AddTransactions(params Transaction[] txs)
	{
		lock (MempoolLock)
		{
			return Mempool.AddMissingTransactions(txs.Select(x => x.GetHash()));
		}
	}

	private async Task<(int Added, int Removed)> MirrorMempoolAsync(CancellationToken cancel)
	{
		// Get all TXIDs in the up-to-date mempool.
		uint256[] txidsNodeMempool = await Rpc.GetRawMempoolAsync(cancel).ConfigureAwait(false);
		ISet<uint256> txidInMemoryMempool = Mempool.GetMempoolTxids();

		var addedHashes = txidsNodeMempool.Except(txidInMemoryMempool).ToList();
		var removedHashes = txidInMemoryMempool.Except(txidsNodeMempool).ToList();

		lock (MempoolLock)
		{
			foreach (var addedHash in addedHashes)
			{
				Mempool.AddTransaction(addedHash);
			}

			foreach (var removedHash in removedHashes)
			{
				Mempool.RemoveTransaction(removedHash);
			}
		}

		return (addedHashes.Count, removedHashes.Count);
	}

	public ISet<uint256> GetMempoolHashes()
	{
		lock (MempoolLock)
		{
			return Mempool.GetMempoolTxids();
		}
	}
}

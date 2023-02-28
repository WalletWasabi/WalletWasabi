using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Stores;

/// <summary>
/// The purpose of this class is to safely and efficiently manage all the Bitcoin related data
/// that's being serialized to disk, like transactions, wallet files, keys, blocks, index files, etc.
/// </summary>
public class BitcoinStore
{
	/// <param name="indexStore">Not initialized index store.</param>
	/// <param name="transactionStore">Not initialized transaction store.</param>
	public BitcoinStore(
		IndexStore indexStore,
		AllTransactionStore transactionStore,
		MempoolService mempoolService,
		IRepository<uint256, Block> blockRepository)
	{
		IndexStore = indexStore;
		TransactionStore = transactionStore;
		MempoolService = mempoolService;
		BlockRepository = blockRepository;
	}

	public IndexStore IndexStore { get; }
	public AllTransactionStore TransactionStore { get; }
	public SmartHeaderChain SmartHeaderChain => IndexStore.SmartHeaderChain;
	public MempoolService MempoolService { get; }
	public IRepository<uint256, Block> BlockRepository { get; }

	/// <summary>
	/// This should not be a property, but a creator function, because it'll be cloned left and right by NBitcoin later.
	/// So it should not be assumed it's some singleton.
	/// </summary>
	public UntrustedP2pBehavior CreateUntrustedP2pBehavior() => new(MempoolService);

	public async Task InitializeAsync(CancellationToken cancel = default)
	{
		using IDisposable _ = BenchmarkLogger.Measure();

		var initTasks = new[]
		{
			IndexStore.InitializeAsync(cancel),
			TransactionStore.InitializeAsync(cancel: cancel)
		};

		await Task.WhenAll(initTasks).ConfigureAwait(false);
	}
}

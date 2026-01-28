using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Stores;

/// <summary>
/// The purpose of this class is to safely and efficiently manage all the Bitcoin related data
/// that's being serialized to disk, like transactions, wallet files, keys, blocks, index files, etc.
/// </summary>
public class BitcoinStore
{
	/// <param name="filterStore">Not initialized filter store.</param>
	/// <param name="transactionStore">Not initialized transaction store.</param>
	public BitcoinStore(
		FilterStore filterStore,
		AllTransactionStore transactionStore,
		MempoolService mempoolService,
		SmartHeaderChain smartHeaderChain,
		FileSystemBlockRepository blockRepository)
	{
		FilterStore = filterStore;
		TransactionStore = transactionStore;
		MempoolService = mempoolService;
		SmartHeaderChain = smartHeaderChain;
		BlockRepository = blockRepository;
	}

	public FilterStore FilterStore { get; }
	public AllTransactionStore TransactionStore { get; }
	public SmartHeaderChain SmartHeaderChain { get; }
	public MempoolService MempoolService { get; }
	public FileSystemBlockRepository BlockRepository { get; }

	/// <summary>
	/// This should not be a property, but a creator function, because it'll be cloned left and right by NBitcoin later.
	/// So it should not be assumed it's some singleton.
	/// </summary>
	public P2pBehavior CreateUntrustedP2pBehavior() => new(MempoolService);

	public async Task InitializeAsync(CancellationToken cancel = default)
	{
		var initTasks = new[]
		{
			FilterStore.InitializeAsync(cancel),
			TransactionStore.InitializeAsync(cancel)
		};

		await Task.WhenAll(initTasks).ConfigureAwait(false);
	}
}

using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.P2p;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// The purpose of this class is to safely and performantly manage all the Bitcoin related data
	/// that's being serialized to disk, like transactions, wallet files, keys, blocks, index files, etc...
	/// </summary>
	public class BitcoinStore
	{
		public BitcoinStore(
			IndexStore indexStore,
			AllTransactionStore transactionStore,
			MempoolService mempoolService)
		{
			IndexStore = Guard.NotNull(nameof(indexStore), indexStore);
			TransactionStore = Guard.NotNull(nameof(transactionStore), transactionStore);
			MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
		}

		/// <summary>
		/// Special constructor used by the mock version.
		/// </summary>
		internal BitcoinStore()
		{
			TransactionStore = new AllTransactionStoreMock();
		}

		public bool IsInitialized { get; private set; }

		public IndexStore IndexStore { get; }
		public AllTransactionStore TransactionStore { get; }
		public SmartHeaderChain SmartHeaderChain => IndexStore.SmartHeaderChain;
		public MempoolService MempoolService { get; }

		/// <summary>
		/// This should not be a property, but a creator function, because it'll be cloned left and right by NBitcoin later.
		/// So it should not be assumed it's some singleton.
		/// </summary>
		public UntrustedP2pBehavior CreateUntrustedP2pBehavior() => new UntrustedP2pBehavior(MempoolService);

		public async Task InitializeAsync()
		{
			using (BenchmarkLogger.Measure())
			{
				var initTasks = new[]
				{
					IndexStore.InitializeAsync(),
					TransactionStore.InitializeAsync()
				};

				await Task.WhenAll(initTasks).ConfigureAwait(false);

				IsInitialized = true;
			}
		}
	}
}

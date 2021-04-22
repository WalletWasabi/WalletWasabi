using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Stores
{
	/// <summary>
	/// The purpose of this class is to safely and efficiently manage all the Bitcoin related data
	/// that's being serialized to disk, like transactions, wallet files, keys, blocks, index files, etc.
	/// </summary>
	public class BitcoinStore : IAsyncDisposable
	{
		public BitcoinStore(
			IndexStore indexStore,
			AllTransactionStore transactionStore,
			IRepository<uint256, Block> blockRepository)
		{
			IndexStore = indexStore;
			TransactionStore = transactionStore;
			BlockRepository = blockRepository;
		}

		public IndexStore IndexStore { get; }
		public AllTransactionStore TransactionStore { get; }
		public SmartHeaderChain SmartHeaderChain => IndexStore.SmartHeaderChain;
		public IRepository<uint256, Block> BlockRepository { get; }

		public async Task InitializeAsync(CancellationToken cancel = default)
		{
			using (BenchmarkLogger.Measure())
			{
				var initTasks = new[]
				{
					IndexStore.InitializeAsync(cancel),
					TransactionStore.InitializeAsync(cancel: cancel)
				};

				await Task.WhenAll(initTasks).ConfigureAwait(false);
			}
		}

		public async ValueTask DisposeAsync()
		{
			await IndexStore.DisposeAsync().ConfigureAwait(false);
			await TransactionStore.DisposeAsync().ConfigureAwait(false);
		}
	}
}

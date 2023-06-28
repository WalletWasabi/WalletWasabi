using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NBitcoin;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Tests.Helpers;
using System.IO;
using System.Linq;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class WalletBuilder : IAsyncDisposable
{
	public WalletBuilder(MockNode node, [CallerMemberName] string callerName = "NN")
	{
		DataDir = Common.GetWorkDir(nameof(WalletSynchronizationTests), callerName);

		IndexStore = new IndexStore(Path.Combine(DataDir, "indexStore"), node.Network, new SmartHeaderChain());

		TransactionStore = new AllTransactionStore(Path.Combine(DataDir, "transactionStore"), node.Network);

		Filters = node.BuildFilters();

		var blockRepositoryMock = new Mock<IRepository<uint256, Block>>();
		blockRepositoryMock
			.Setup(br => br.TryGetAsync(It.IsAny<uint256>(), It.IsAny<CancellationToken>()))
			.Returns((uint256 hash, CancellationToken _) => Task.FromResult(node.BlockChain[hash])!);
		blockRepositoryMock
			.Setup(br => br.SaveAsync(It.IsAny<Block>(), It.IsAny<CancellationToken>()))
			.Returns((Block _, CancellationToken _) => Task.CompletedTask);

		BitcoinStore = new BitcoinStore(IndexStore, TransactionStore, new MempoolService(), blockRepositoryMock.Object);
		Cache = new MemoryCache(new MemoryCacheOptions());
		HttpClientFactory = new HttpClientFactory(torEndPoint: null, backendUriGetter: () => null!);
	}

	private IndexStore IndexStore { get; }
	private AllTransactionStore TransactionStore { get; }
	private BitcoinStore BitcoinStore { get; }
	private MemoryCache Cache { get; }
	private HttpClientFactory HttpClientFactory { get; }
	public IEnumerable<FilterModel> Filters { get; }
	public string DataDir { get; }

	public async Task<WalletWasabi.Wallets.Wallet> CreateRealWalletBasedOnTestWalletAsync(TestWallet wallet)
	{
		await BitcoinStore.InitializeAsync(); // StartingFilter already added to IndexStore after this line.

		await BitcoinStore.IndexStore.AddNewFiltersAsync(Filters.Skip(1));
		var keyManager = KeyManager.CreateNewWatchOnly(wallet.GetSegwitAccountExtPubKey(), null!);
		keyManager.GetKeys(_ => true); // Make sure keys are asserted.

		var serviceConfiguration = new ServiceConfiguration(new UriEndPoint(new Uri("http://www.nomatter.dontcare")), Money.Coins(WalletWasabi.Helpers.Constants.DefaultDustThreshold));
		WasabiSynchronizer synchronizer = new(requestInterval: TimeSpan.FromSeconds(3), 1000, BitcoinStore, HttpClientFactory);
		HybridFeeProvider feeProvider = new(synchronizer, null);
		SmartBlockProvider blockProvider = new(BitcoinStore.BlockRepository, rpcBlockProvider: null, null, null, Cache);

		return WalletWasabi.Wallets.Wallet.CreateAndRegisterServices(Network.RegTest, BitcoinStore, keyManager, synchronizer, DataDir, serviceConfiguration, feeProvider, blockProvider);
	}

	public async ValueTask DisposeAsync()
	{
		await IndexStore.DisposeAsync();
		await TransactionStore.DisposeAsync();
		await HttpClientFactory.DisposeAsync();
		Cache.Dispose();
	}
}

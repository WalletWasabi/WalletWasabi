using System.Collections.Generic;
using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Indexer.Models;
using WalletWasabi.Indexer.Models.Responses;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using FiltersResponse = WalletWasabi.WebClients.Wasabi.FiltersResponse;

namespace WalletWasabi.Tests.RegressionTests;

public class RegTestSetup : IAsyncDisposable
{
	[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "The variable must be a field unless refactored.")]
	public long FiltersProcessedByWalletCount;

	public RegTestSetup(RegTestFixture regTestFixture, string dir)
	{
		RegTestFixture = regTestFixture;
		ServiceConfiguration = new ServiceConfiguration(regTestFixture.IndexerRegTestNode.P2pEndPoint.ToUri("http").ToString(), Money.Coins(Constants.DefaultDustThreshold));

		EventBus = new EventBus();
		SmartHeaderChain smartHeaderChain = new();
		IndexStore = new IndexStore(Path.Combine(dir, "indexStore"), Network, smartHeaderChain);
		TransactionStore = new AllTransactionStore(Path.Combine(dir, "transactionStore"), Network);
		MempoolService mempoolService = new();
		FileSystemBlockRepository blocks = new(Path.Combine(dir, "blocks"), Network);
		BitcoinStore = new BitcoinStore(IndexStore, TransactionStore, mempoolService, smartHeaderChain, blocks);
	}

	public RegTestFixture RegTestFixture { get; }
	public IndexStore IndexStore { get; }
	public BitcoinStore BitcoinStore { get; }
	public AllTransactionStore TransactionStore { get; }
	public IRPCClient RpcClient => RegTestFixture.IndexerRegTestNode.RpcClient;
	public Network Network => RpcClient.Network;
	public ServiceConfiguration ServiceConfiguration { get; }
	public EventBus EventBus { get; }
	public string Password { get; } = "password";

	public static async Task<RegTestSetup> InitializeTestEnvironmentAsync(
		RegTestFixture regTestFixture,
		[CallerFilePath] string callerFilePath = "",
		[CallerMemberName] string callerMemberName = "")
	{
		string dir = Helpers.Common.GetWorkDir(callerFilePath, callerMemberName);
		RegTestSetup setup = new(regTestFixture, dir);
		await setup.RpcClient.GenerateAsync(101).ConfigureAwait(false); // Make sure everything is confirmed.
		await setup.AssertFiltersInitializedAsync().ConfigureAwait(false); // Make sure filters are created on the server side.

		await setup.BitcoinStore.InitializeAsync().ConfigureAwait(false);

		return setup;
	}

	public async Task AssertFiltersInitializedAsync()
	{
		uint256 firstHash = await RpcClient.GetBlockHashAsync(0).ConfigureAwait(false);

		while (true)
		{
			var client = new IndexerClient(RegTestFixture.IndexerHttpClientFactory.CreateClient("test"));
			var filtersResponse = await client.GetFiltersAsync(firstHash, 1000).ConfigureAwait(false);
			Assert.NotNull(filtersResponse);

			if (filtersResponse is FiltersResponse.AlreadyOnBestBlock)
			{
				break;
			}

			await Task.Delay(100).ConfigureAwait(false);
		}
	}

	public async Task WaitForFiltersToBeProcessedAsync(TimeSpan timeout, int numberOfFiltersToWaitFor)
	{
		var times = 0;
		while (Interlocked.Read(ref FiltersProcessedByWalletCount) < numberOfFiltersToWaitFor)
		{
			if (times > timeout.TotalSeconds)
			{
				throw new TimeoutException($"{nameof(Wallet)} test timed out. Filter was not processed. Needed: {numberOfFiltersToWaitFor}, got only: {Interlocked.Read(ref FiltersProcessedByWalletCount)}.");
			}
			await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
			times++;
		}
	}

	public void Wallet_NewFiltersProcessed(object? sender, IEnumerable<FilterModel> filters)
	{
		foreach (var _ in filters)
		{
			Interlocked.Increment(ref FiltersProcessedByWalletCount);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await IndexStore.DisposeAsync().ConfigureAwait(false);
		await TransactionStore.DisposeAsync().ConfigureAwait(false);
	}
}

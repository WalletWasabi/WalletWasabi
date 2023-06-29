using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

public class RegTestSetup : IAsyncDisposable
{
	[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "The variable must be a field unless refactored.")]
	public long FiltersProcessedByWalletCount;

	public RegTestSetup(RegTestFixture regTestFixture, string dir)
	{
		RegTestFixture = regTestFixture;
		ServiceConfiguration = new ServiceConfiguration(regTestFixture.BackendRegTestNode.P2pEndPoint, Money.Coins(Constants.DefaultDustThreshold));

		IndexStore = new IndexStore(Path.Combine(dir, "indexStore"), Network, new SmartHeaderChain());
		TransactionStore = new AllTransactionStore(Path.Combine(dir, "transactionStore"), Network);
		MempoolService mempoolService = new();
		FileSystemBlockRepository blocks = new(Path.Combine(dir, "blocks"), Network);
		BitcoinStore = new BitcoinStore(IndexStore, TransactionStore, mempoolService, blocks);
	}

	public RegTestFixture RegTestFixture { get; }
	public Global Global => RegTestFixture.Global;
	public IndexStore IndexStore { get; }
	public BitcoinStore BitcoinStore { get; }
	public AllTransactionStore TransactionStore { get; }
	public IRPCClient RpcClient => Global.RpcClient!;
	public Network Network => RpcClient.Network;
	public Coordinator Coordinator => Global.Coordinator!;
	public ServiceConfiguration ServiceConfiguration { get; }
	public string Password { get; } = "password";

	public static async Task<RegTestSetup> InitializeTestEnvironmentAsync(
		RegTestFixture regTestFixture,
		int numberOfBlocksToGenerate,
		[CallerFilePath] string callerFilePath = "",
		[CallerMemberName] string callerMemberName = "")
	{
		string dir = Helpers.Common.GetWorkDir(callerFilePath, callerMemberName);
		RegTestSetup setup = new(regTestFixture, dir);
		await setup.AssertFiltersInitializedAsync().ConfigureAwait(false); // Make sure filters are created on the server side.

		if (numberOfBlocksToGenerate != 0)
		{
			await setup.RpcClient.GenerateAsync(numberOfBlocksToGenerate).ConfigureAwait(false); // Make sure everything is confirmed.
		}

		setup.Coordinator.UtxoReferee.Clear();

		await setup.BitcoinStore.InitializeAsync().ConfigureAwait(false);

		return setup;
	}

	public async Task AssertFiltersInitializedAsync()
	{
		uint256 firstHash = await Global.RpcClient.GetBlockHashAsync(0).ConfigureAwait(false);

		while (true)
		{
			var client = new WasabiClient(RegTestFixture.BackendHttpClient);
			FiltersResponse? filtersResponse = await client.GetFiltersAsync(firstHash, 1000).ConfigureAwait(false);
			Assert.NotNull(filtersResponse);

			var filterCount = filtersResponse!.Filters.Count();
			if (filterCount >= 101)
			{
				break;
			}
			else
			{
				await Task.Delay(100).ConfigureAwait(false);
			}
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

	public void Wallet_NewFilterProcessed(object? sender, FilterModel e)
	{
		Interlocked.Increment(ref FiltersProcessedByWalletCount);
	}

	public async ValueTask DisposeAsync()
	{
		await IndexStore.DisposeAsync().ConfigureAwait(false);
		await TransactionStore.DisposeAsync().ConfigureAwait(false);
	}
}

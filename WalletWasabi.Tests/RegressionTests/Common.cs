using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.Models;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor.Http;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests
{
	public static class Common
	{
		public static long FiltersProcessedByWalletCount;

		public static async Task WaitForFiltersToBeProcessedAsync(TimeSpan timeout, int numberOfFiltersToWaitFor)
		{
			var times = 0;
			while (Interlocked.Read(ref FiltersProcessedByWalletCount) < numberOfFiltersToWaitFor)
			{
				if (times > timeout.TotalSeconds)
				{
					throw new TimeoutException($"{nameof(Wallet)} test timed out. Filter was not processed. Needed: {numberOfFiltersToWaitFor}, got only: {Interlocked.Read(ref FiltersProcessedByWalletCount)}.");
				}
				await Task.Delay(TimeSpan.FromSeconds(1));
				times++;
			}
		}

		public static void Wallet_NewFilterProcessed(object? sender, FilterModel e)
		{
			Interlocked.Increment(ref FiltersProcessedByWalletCount);
		}

		private static async Task AssertFiltersInitializedAsync(RegTestFixture regTestFixture, Backend.Global global)
		{
			var firstHash = await global.RpcClient.GetBlockHashAsync(0);
			while (true)
			{
				var client = new WasabiClient(regTestFixture.BackendHttpClient);
				FiltersResponse? filtersResponse = await client.GetFiltersAsync(firstHash, 1000);
				Assert.NotNull(filtersResponse);

				var filterCount = filtersResponse!.Filters.Count();
				if (filterCount >= 101)
				{
					break;
				}
				else
				{
					await Task.Delay(100);
				}
			}
		}

		public static async Task<(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global)> InitializeTestEnvironmentAsync(
			RegTestFixture regTestFixture,
			int numberOfBlocksToGenerate,
			[CallerFilePath] string callerFilePath = "",
			[CallerMemberName] string callerMemberName = "")
		{
			var global = regTestFixture.Global;
			await AssertFiltersInitializedAsync(regTestFixture, global); // Make sure filters are created on the server side.
			if (numberOfBlocksToGenerate != 0)
			{
				await global.RpcClient.GenerateAsync(numberOfBlocksToGenerate); // Make sure everything is confirmed.
			}
			global.Coordinator.UtxoReferee.Clear();

			var network = global.RpcClient.Network;
			var serviceConfiguration = new ServiceConfiguration(MixUntilAnonymitySet.PrivacyLevelSome.ToString(), 2, 21, 50, regTestFixture.BackendRegTestNode.P2pEndPoint, Money.Coins(WalletWasabi.Helpers.Constants.DefaultDustThreshold));

			var dir = Helpers.Common.GetWorkDir(callerFilePath, callerMemberName);
			var indexStore = new IndexStore(Path.Combine(dir, "indexStore"), network, new SmartHeaderChain());
			var transactionStore = new AllTransactionStore(Path.Combine(dir, "transactionStore"), network);
			var mempoolService = new MempoolService();
			var blocks = new FileSystemBlockRepository(Path.Combine(dir, "blocks"), network);
			var bitcoinStore = new BitcoinStore(indexStore, transactionStore, mempoolService, blocks);
			await bitcoinStore.InitializeAsync();
			return ("password", global.RpcClient, network, global.Coordinator, serviceConfiguration, bitcoinStore, global);
		}
	}
}

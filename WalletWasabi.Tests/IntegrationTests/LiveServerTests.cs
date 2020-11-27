using NBitcoin;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.Models;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	[Collection("LiveServerTests collection")]
	public class LiveServerTests : IAsyncLifetime
	{
		public LiveServerTests(LiveServerTestsFixture liveServerTestsFixture)
		{
			LiveServerTestsFixture = liveServerTestsFixture;
			TorSocks5ClientPool = new TorSocks5ClientPool(Common.TorSocks5Endpoint);
		}

		public async Task InitializeAsync()
		{
			var torManager = new TorProcessManager(Common.TorSettings, Common.TorSocks5Endpoint);
			bool started = await torManager.StartAsync(ensureRunning: true);
			Assert.True(started, "Tor failed to start.");
		}

		public Task DisposeAsync()
		{
			return Task.CompletedTask;
		}

		private LiveServerTestsFixture LiveServerTestsFixture { get; }
		public TorSocks5ClientPool TorSocks5ClientPool { get; }

		#region Blockchain

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFiltersAsync(NetworkType networkType)
		{
			Network network = (networkType == NetworkType.Mainnet) ? Network.Main : Network.TestNet;

			var torHttpClient = MakeTorHttpClient(networkType);
			var client = new WasabiClient(torHttpClient);

			var filterModel = StartingFilters.GetStartingFilter(network);

			FiltersResponse? filtersResponse = await client.GetFiltersAsync(filterModel.Header.BlockHash, 2);

			Assert.NotNull(filtersResponse);
			Assert.True(filtersResponse!.Filters.Count() == 2);
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetAllRoundStatesAsync(NetworkType networkType)
		{
			var torHttpClient = MakeTorHttpClient(networkType);
			var client = new SatoshiClient(torHttpClient);
			var states = await client.GetAllRoundStatesAsync();
			Assert.True(states.NotNullAndNotEmpty());
			Assert.True(states.Count() >= 1);
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetTransactionsAsync(NetworkType networkType)
		{
			var torHttpClient = MakeTorHttpClient(networkType);
			var client = new WasabiClient(torHttpClient);

			var randomTxIds = Enumerable.Range(0, 20).Select(_ => RandomUtils.GetUInt256());
			var network = networkType == NetworkType.Mainnet ? Network.Main : Network.TestNet;

			var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
				await client.GetTransactionsAsync(network, randomTxIds.Take(4), CancellationToken.None));
			Assert.Equal("Bad Request\nNo such mempool or blockchain transaction. Use gettransaction for wallet transactions.", ex.Message);

			var mempoolTxIds = await client.GetMempoolHashesAsync(CancellationToken.None);
			randomTxIds = Enumerable.Range(0, 5).Select(_ => mempoolTxIds.RandomElement()).Distinct().ToArray();
			var txs = await client.GetTransactionsAsync(network, randomTxIds, CancellationToken.None);
			var returnedTxIds = txs.Select(tx => tx.GetHash());
			Assert.Equal(returnedTxIds.OrderBy(x => x).ToArray(), randomTxIds.OrderBy(x => x).ToArray());
		}

		#endregion Blockchain

		#region Offchain

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetExchangeRateAsync(NetworkType networkType) // xunit wtf: If this function is called GetExchangeRatesAsync, it'll stuck on 1 CPU VMs (Manjuro, Fedora)
		{
			var torHttpClient = MakeTorHttpClient(networkType);
			var client = new WasabiClient(torHttpClient);

			var exchangeRates = await client.GetExchangeRatesAsync();

			Assert.True(exchangeRates.NotNullAndNotEmpty());
		}

		#endregion Offchain

		#region Software

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetVersionsTestsAsync(NetworkType networkType)
		{
			var torHttpClient = MakeTorHttpClient(networkType);
			var client = new WasabiClient(torHttpClient);

			var versions = await client.GetVersionsAsync(CancellationToken.None);
			Assert.InRange(versions.ClientVersion, new Version(1, 1, 10), new Version(1, 2));
			Assert.InRange(versions.ClientVersion, new Version(1, 1, 10), WalletWasabi.Helpers.Constants.ClientVersion);
			Assert.Equal(4, versions.BackendMajorVersion);
			Assert.Equal(new Version(2, 0), versions.LegalDocumentsVersion);
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task CheckUpdatesTestsAsync(NetworkType networkType)
		{
			var torHttpClient = MakeTorHttpClient(networkType);
			var client = new WasabiClient(torHttpClient);

			var updateStatus = await client.CheckUpdatesAsync(CancellationToken.None);

			var expectedVersion = new Version(2, 0);
			ushort backendVersion = 4;
			Assert.Equal(new UpdateStatus(true, true, expectedVersion, backendVersion), updateStatus);
			Assert.True(updateStatus.BackendCompatible);
			Assert.True(updateStatus.ClientUpToDate);
			Assert.Equal(expectedVersion, updateStatus.LegalDocumentsVersion);
			Assert.Equal(backendVersion, updateStatus.CurrentBackendMajorVersion);

			var versions = await client.GetVersionsAsync(CancellationToken.None);
			Assert.Equal(versions.LegalDocumentsVersion, updateStatus.LegalDocumentsVersion);
		}

		#endregion Software

		#region Wasabi

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetLegalDocumentsTestsAsync(NetworkType networkType)
		{
			var torHttpClient = MakeTorHttpClient(networkType);
			var client = new WasabiClient(torHttpClient);

			var content = await client.GetLegalDocumentsAsync(CancellationToken.None);

			var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
			Assert.Equal("Last Updated: 2020-04-05", lines[0]);
			var lineCount = lines.Length;
			Assert.InRange(lineCount, 100, 1000);
		}

		#endregion Wasabi

		private TorHttpClient MakeTorHttpClient(NetworkType networkType)
		{
			Uri baseUri = LiveServerTestsFixture.UriMappings[networkType];
			return new TorHttpClient(TorSocks5ClientPool, () => baseUri);
		}
	}
}

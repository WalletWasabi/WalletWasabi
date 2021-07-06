using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.Models;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
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

			TorHttpPool = new(new TorTcpConnectionFactory(Common.TorSocks5Endpoint));
			TorManager = new(Common.TorSettings);
		}

		private TorProcessManager TorManager { get; }
		private TorHttpPool TorHttpPool { get; }
		private LiveServerTestsFixture LiveServerTestsFixture { get; }

		public async Task InitializeAsync()
		{
			await TorManager.StartAsync();
		}

		public async Task DisposeAsync()
		{
			TorHttpPool.Dispose();
			await TorManager.DisposeAsync();
		}

		#region Blockchain

		[Theory]
		[MemberData(nameof(GetNetworks))]
		public async Task GetFiltersAsync(Network network)
		{
			TorHttpClient torHttpClient = MakeTorHttpClient(network);
			WasabiClient client = new(torHttpClient);

			var filterModel = StartingFilters.GetStartingFilter(network);

			FiltersResponse? filtersResponse = await client.GetFiltersAsync(filterModel.Header.BlockHash, 2);

			Assert.NotNull(filtersResponse);
			Assert.True(filtersResponse!.Filters.Count() == 2);
		}

		[Theory]
		[MemberData(nameof(GetNetworks))]
		public async Task GetAllRoundStatesAsync(Network network)
		{
			TorHttpClient torHttpClient = MakeTorHttpClient(network);
			SatoshiClient client = new(torHttpClient);
			var states = await client.GetAllRoundStatesAsync();
			Assert.True(states.NotNullAndNotEmpty());
			Assert.True(states.Any());
		}

		[Theory]
		[MemberData(nameof(GetNetworks))]
		public async Task GetTransactionsAsync(Network network)
		{
			TorHttpClient torHttpClient = MakeTorHttpClient(network);
			WasabiClient client = new(torHttpClient);

			IEnumerable<uint256> randomTxIds = Enumerable.Range(0, 20).Select(_ => RandomUtils.GetUInt256());

			var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
				await client.GetTransactionsAsync(network, randomTxIds.Take(4), CancellationToken.None));
			Assert.Equal("Bad Request\nNo such mempool or blockchain transaction. Use gettransaction for wallet transactions.", ex.Message);

			var mempoolTxIds = await client.GetMempoolHashesAsync(CancellationToken.None);
			randomTxIds = Enumerable.Range(0, 5).Select(_ => mempoolTxIds.RandomElement()!).Distinct().ToArray();
			var txs = await client.GetTransactionsAsync(network, randomTxIds, CancellationToken.None);
			var returnedTxIds = txs.Select(tx => tx.GetHash());
			Assert.Equal(returnedTxIds.OrderBy(x => x).ToArray(), randomTxIds.OrderBy(x => x).ToArray());
		}

		#endregion Blockchain

		#region Offchain

		[Theory]
		[MemberData(nameof(GetNetworks))]
		public async Task GetExchangeRateAsync(Network network)
		{
			TorHttpClient torHttpClient = MakeTorHttpClient(network);
			WasabiClient client = new(torHttpClient);

			var exchangeRates = await client.GetExchangeRatesAsync();

			Assert.True(exchangeRates.NotNullAndNotEmpty());
		}

		#endregion Offchain

		#region Software

		[Theory]
		[MemberData(nameof(GetNetworks))]
		public async Task GetVersionsTestsAsync(Network network)
		{
			TorHttpClient torHttpClient = MakeTorHttpClient(network);
			WasabiClient client = new(torHttpClient);

			var versions = await client.GetVersionsAsync(CancellationToken.None);
			Assert.InRange(versions.ClientVersion, new(1, 1, 10), new(1, 2));
			Assert.InRange(versions.ClientVersion, new(1, 1, 10), WalletWasabi.Helpers.Constants.ClientVersion);
			Assert.Equal(4, versions.BackendMajorVersion);
			Assert.Equal(new(2, 0), versions.LegalDocumentsVersion);
		}

		[Theory]
		[MemberData(nameof(GetNetworks))]
		public async Task CheckUpdatesTestsAsync(Network network)
		{
			TorHttpClient torHttpClient = MakeTorHttpClient(network);
			WasabiClient client = new(torHttpClient);

			var updateStatus = await client.CheckUpdatesAsync(CancellationToken.None);

			Version expectedVersion = new(2, 0);
			ushort backendVersion = 4;
			Assert.Equal(new(true, true, expectedVersion, backendVersion), updateStatus);
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
		[MemberData(nameof(GetNetworks))]
		public async Task GetLegalDocumentsTestsAsync(Network network)
		{
			TorHttpClient torHttpClient = MakeTorHttpClient(network);
			WasabiClient client = new(torHttpClient);

			var content = await client.GetLegalDocumentsAsync(CancellationToken.None);

			var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
			Assert.Equal("Last Updated: 2020-04-05", lines[0]);
			var lineCount = lines.Length;
			Assert.InRange(lineCount, 100, 1000);
		}

		#endregion Wasabi

		private TorHttpClient MakeTorHttpClient(Network network)
		{
			Uri baseUri = LiveServerTestsFixture.UriMappings[network];
			return new(baseUri, TorHttpPool);
		}

		public static IEnumerable<object[]> GetNetworks()
		{
			yield return new object[] { Network.Main };
			yield return new object[] { Network.TestNet };
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.ChaumianCoinJoin;
using Xunit;

namespace WalletWasabi.Tests
{
	[Collection("LiveServerTests collection")]
	public class LiveServerTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		private LiveServerTestsFixture LiveServerTestsFixture { get; }

		public LiveServerTests(SharedFixture sharedFixture, LiveServerTestsFixture liveServerTestsFixture)
		{
			SharedFixture = sharedFixture;
			LiveServerTestsFixture = liveServerTestsFixture;
		}

		// Blockchain
		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFeesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType]))
			{
				var feeEstimationPairs = await client.GetFeesAsync(1000);

				Assert.True(feeEstimationPairs.NotNullAndNotEmpty());
			}
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFiltersAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType]))
			{
				var filterModel = IndexDownloader.GetStartingFilter(Network.GetNetwork(networkType.ToString()));

				var filters = await client.GetFiltersAsync(filterModel.BlockHash, 2);

				Assert.True(filters.NotNullAndNotEmpty());
				Assert.True(filters.Count() == 2);
			}
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetAllRoundStatesAsync(NetworkType networkType)
		{
			using (var client = new SatoshiClient(LiveServerTestsFixture.UriMappings[networkType]))
			{
				var states = await client.GetAllRoundStatesAsync();
				Assert.True(states.NotNullAndNotEmpty());
				Assert.True(states.Count() >= 1);
			}
		}

		// ChaumianCoinJoin
		[Theory]
		[InlineData(NetworkType.Testnet)]
		public async Task RegisterAliceInputAsync(NetworkType networkType)
		{
			Network network = Network.GetNetwork(networkType.ToString());

			var activeOutputAddress = LiveServerTestsFixture.AliceInputMappings[networkType].activeOutputAddress;
			var changeOutputAddress = LiveServerTestsFixture.AliceInputMappings[networkType].changeOutputAddress;

			// blinded data created using activeOutputAddress ScriptPubKey
			var blindedDataAsHex = LiveServerTestsFixture.AliceInputMappings[networkType].blindedDataHex;

			// signed with private key that owns the utxos
			var proof = LiveServerTestsFixture.AliceInputMappings[networkType].proof;

			byte[] blindedData = ByteHelpers.FromHex(blindedDataAsHex);

			// utxos
			var txoRefs = LiveServerTestsFixture.AliceInputMappings[networkType].utxos;

			var inputProofModels = txoRefs.Select(txrf => new InputProofModel
			{
				Input = new TxoRef(txrf.TransactionId, txrf.Index),
				Proof = proof
			});

			var aliceClient = await AliceClient.CreateNewAsync(changeOutputAddress, blindedData, inputProofModels, LiveServerTestsFixture.UriMappings[networkType]);

			Assert.NotNull(aliceClient?.RoundId);
			Assert.NotNull(aliceClient?.UniqueId);
			Assert.NotNull(aliceClient?.BlindedOutputSignature);

			// need to uncofirm or test will fail when run again
			await aliceClient.PostUnConfirmationAsync();
		}

		// Offchain
		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetExchangeRatesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType]))
			{
				var exchangeRates = await client.GetExchangeRatesAsync();

				Assert.True(exchangeRates.NotNullAndNotEmpty());
			}
		}
	}
}

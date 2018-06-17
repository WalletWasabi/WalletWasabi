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
	public class LiveServerTests : IClassFixture<SharedFixture>
	{
		private readonly Dictionary<NetworkType, Uri> _networkUriMappings = new Dictionary<NetworkType, Uri>
		{
				{ NetworkType.Mainnet, new Uri("http://4jsmnfcsmbrlm7l7.onion") },
				{ NetworkType.Testnet, new Uri("http://wtgjmaol3io5ijii.onion") }
		};

		// Blockchain
		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFeesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(_networkUriMappings[networkType]))
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
			using (var client = new WasabiClient(_networkUriMappings[networkType]))
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
			using (var client = new SatoshiClient(_networkUriMappings[networkType]))
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

			var activeOutputAddress = new BitcoinPubKeyAddress("mrEadRbqnbFm7gvqFRisUvqh31tMPy9EGJ");
			var changeOutputAddress = new BitcoinPubKeyAddress("mqjeEPQuPVEacfAgfnkhtkbFAYjxRrLUre");

			// blinded data created using activeOutputAddress ScriptPubKey
			var blindedDataAsHex = "546B0901BF3CBA6A694B89A2914C3970FB3BA45EA4E917D049F6A4B5DF84E39B84EB35FF2295F65DE10C95E0280F78B8C4177132807C14A9F0C0358614BA5708F119F161AE05199AC465EF734C2C77D0719AA10BA03F9541E81FF6E80587AAA27B09962BF44FBAA6C2FD3F4C6E2778D5220ED522D1902F73CEC8627E08DE9062E7DF815F1DBD538C801211C0A602938851FC38C2DC9166F51904340386067E5983189249B2420D2CA0838AE2EA3F4AD2445B00245F2F5AF1C34CAEC403C8B1BDAB46F78BFF60F939AB5441A36A31E0EE3D4BF8E46DD67B036AC009A053BCAD7CF0DA1482239DA3559150E0620011A198AE3215ADC3C2F6E50B85767BA61AB1A9";

			// signed with private key that owns the utxos
			var proof = "H+qI1XYOKWL1x1MQjEzHWkKIX2o3pKzSauY99e7rncTCUhIPwHYvL0END6gVs7rhoXZHgVB0IZOipZYtCAkbMtM=";

			byte[] blindedData = ByteHelpers.FromHex(blindedDataAsHex);

			// utxos
			var txoRefs = new List<TxoRef>
			{
				new TxoRef(new uint256("32914e4466c2ba0328bcac6102d5a806b3e44d5ed73b2454ecae730bebcf8784"), 0 ),
				new TxoRef(new uint256("ac0e02035e885fe0ea921f7a7bbbdb362378ed0800fbe0e54af676a8ab0df710"), 0 ),
				new TxoRef(new uint256("72b8b61f9bb57519cda99458f4b3fb9881142492b9c55ce50ec70bde42f419ed"), 0 ),
				new TxoRef( new uint256("ec123d68e3cafcd648ae5258f1587afb367eb6a3bc4275d50fcacbad9e27b12e"), 0 ),
				new TxoRef(new uint256("6451fbf9b39e4ca649a36e71381b45e314526aa0074dc055eab39de581aeedfb"), 0 ),
				new TxoRef(new uint256("b65a812be86078ac5b5d2a531aee6197a628d60034dbde9cf035d5a5d8bd064f"), 0 )
			};

			var inputProofModels = txoRefs.Select(txrf => new InputProofModel
			{
				Input = new Models.TxoRef(txrf.TransactionId, txrf.Index),
				Proof = proof
			});

			var aliceClient = await AliceClient.CreateNewAsync(changeOutputAddress, blindedData, inputProofModels, _networkUriMappings[networkType]);

			Assert.NotNull(aliceClient?.RoundId);
			Assert.NotNull(aliceClient?.UniqueId);
			Assert.NotNull(aliceClient?.BlindedOutputSignature);
		}

		// Offchain
		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetExchangeRatesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(_networkUriMappings[networkType]))
			{
				var exchangeRates = await client.GetExchangeRatesAsync();

				Assert.True(exchangeRates.NotNullAndNotEmpty());
			}
		}
	}
}

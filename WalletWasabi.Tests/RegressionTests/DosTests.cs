using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.Backend.Controllers;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Crypto;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.TorSocks5;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using static WalletWasabi.Crypto.SchnorrBlinding;
using UnblindedSignature = WalletWasabi.Crypto.UnblindedSignature;

namespace WalletWasabi.Tests.RegressionTests
{
	[Collection("RegTest collection")]
	public class DosTests
	{
#pragma warning disable IDE0059 // Value assigned to symbol is never used

		public DosTests(RegTestFixture regTestFixture)
		{
			RegTestFixture = regTestFixture;
		}

		private RegTestFixture RegTestFixture { get; }

		private static async Task WaitForTimeoutAsync(Uri baseUri)
		{
			using var satoshiClient = new SatoshiClient(baseUri, null);
			var times = 0;
			while (!(await satoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == RoundPhase.InputRegistration))
			{
				await Task.Delay(100);
				if (times > 50) // 5 sec, 3 should be enough
				{
					throw new TimeoutException("Not all rounds were in InputRegistration.");
				}
				times++;
			}
		}

		[Fact]
		public async Task BanningTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 3;
			int connectionConfirmationTimeout = 120;
			var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 1, 1, 1, 24, true, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");

			await rpc.GenerateAsync(3); // So to make sure we have enough money.

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);
			var fundingTxCount = 0;
			var inputRegistrationUsers = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData)>();
			CoordinatorRound round = null;
			for (int i = 0; i < roundConfig.AnonymitySet; i++)
			{
				var userInputData = new List<(Key key, BitcoinWitPubKeyAddress inputAddress, uint256 txHash, Transaction tx, OutPoint input)>();
				var activeOutputAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				var changeOutputAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				Requester requester = new Requester();
				uint256 blinded = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, activeOutputAddress.ScriptPubKey);
				uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blinded.ToBytes()));

				var inputProofModels = new List<InputProofModel>();
				int numberOfInputs = new Random().Next(1, 7);
				var receiveSatoshiSum = 0;
				for (int j = 0; j < numberOfInputs; j++)
				{
					var key = new Key();
					var receiveSatoshi = new Random().Next(1000, 100000000);
					receiveSatoshiSum += receiveSatoshi;
					if (j == numberOfInputs - 1)
					{
						receiveSatoshi = 100000000;
					}
					BitcoinWitPubKeyAddress inputAddress = key.PubKey.GetSegwitAddress(network);
					uint256 txHash = await rpc.SendToAddressAsync(inputAddress, Money.Satoshis(receiveSatoshi));
					fundingTxCount++;
					Assert.NotNull(txHash);
					Transaction transaction = await rpc.GetRawTransactionAsync(txHash);

					var coin = transaction.Outputs.GetCoins(inputAddress.ScriptPubKey).Single();

					OutPoint input = coin.Outpoint;
					var inputProof = new InputProofModel { Input = input, Proof = key.SignCompact(blindedOutputScriptsHash) };
					inputProofModels.Add(inputProof);

					GetTxOutResponse getTxOutResponse = await rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true);
					// Check if inputs are unspent.
					Assert.NotNull(getTxOutResponse);

					userInputData.Add((key, inputAddress, txHash, transaction, input));
				}

				inputRegistrationUsers.Add((requester, blinded, activeOutputAddress, changeOutputAddress, inputProofModels, userInputData));
			}

			var mempool = await rpc.GetRawMempoolAsync();
			Assert.Equal(inputRegistrationUsers.SelectMany(x => x.userInputData).Count(), mempool.Length);

			while ((await rpc.GetRawMempoolAsync()).Length != 0)
			{
				await rpc.GenerateAsync(1);
			}

			var aliceClients = new List<Task<AliceClient>>();

			foreach (var user in inputRegistrationUsers)
			{
				aliceClients.Add(AliceClientBase.CreateNewAsync(round.RoundId, new[] { user.activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey }, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, () => baseUri, null));
			}

			long roundId = 0;
			var users = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient aliceClient, UnblindedSignature unblindedSignature)>();
			for (int i = 0; i < inputRegistrationUsers.Count; i++)
			{
				var user = inputRegistrationUsers[i];
				var request = aliceClients[i];

				var aliceClient = await request;

				if (roundId == 0)
				{
					roundId = aliceClient.RoundId;
				}
				else
				{
					Assert.Equal(roundId, aliceClient.RoundId);
				}
				// Because it's valuetuple.
				users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient, null));
			}

			Assert.Equal(users.Count, roundConfig.AnonymitySet);

			var confirmationRequests = new List<Task<(RoundPhase currentPhase, IEnumerable<ActiveOutput>)>>();

			foreach (var user in users)
			{
				confirmationRequests.Add(user.aliceClient.PostConfirmationAsync());
			}

			RoundPhase roundPhase = RoundPhase.InputRegistration;
			int k = 0;
			foreach (var request in confirmationRequests)
			{
				var resp = await request;
				if (roundPhase == RoundPhase.InputRegistration)
				{
					roundPhase = resp.currentPhase;
				}
				else
				{
					Assert.Equal(roundPhase, resp.currentPhase);
				}

				var user = users.ElementAt(k);
				user.unblindedSignature = resp.Item2.First().Signature;
			}

			using (var satoshiClient = new SatoshiClient(baseUri, null))
			{
				var times = 0;
				while (!(await satoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == RoundPhase.InputRegistration))
				{
					await Task.Delay(100);
					if (times > 50) // 5 sec, 3 should be enough
					{
						throw new TimeoutException("Not all rounds were in InputRegistration.");
					}
					times++;
				}
			}

			int bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.Equal(0, bannedCount);

			aliceClients.Clear();
			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			foreach (var user in inputRegistrationUsers)
			{
				aliceClients.Add(AliceClientBase.CreateNewAsync(round.RoundId, new[] { user.activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey }, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, () => baseUri, null));
			}

			roundId = 0;
			users = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient aliceClient, UnblindedSignature unblindedSignature)>();
			for (int i = 0; i < inputRegistrationUsers.Count; i++)
			{
				var user = inputRegistrationUsers[i];
				var request = aliceClients[i];

				var aliceClient = await request;
				if (roundId == 0)
				{
					roundId = aliceClient.RoundId;
				}
				else
				{
					Assert.Equal(roundId, aliceClient.RoundId);
				}
				// Because it's valuetuple.
				users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient, null));
			}

			Assert.Equal(users.Count, roundConfig.AnonymitySet);

			confirmationRequests = new List<Task<(RoundPhase currentPhase, IEnumerable<ActiveOutput>)>>();

			foreach (var user in users)
			{
				confirmationRequests.Add(user.aliceClient.PostConfirmationAsync());
			}

			using (var satoshiClient = new SatoshiClient(baseUri, null))
			{
				var times = 0;
				while (!(await satoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == RoundPhase.InputRegistration))
				{
					await Task.Delay(100);
					if (times > 50) // 5 sec, 3 should be enough
					{
						throw new TimeoutException("Not all rounds were in InputRegistration.");
					}
					times++;
				}
			}

			bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.True(bannedCount >= roundConfig.AnonymitySet);

			foreach (var aliceClient in aliceClients)
			{
				aliceClient?.Dispose();
			}
		}

		[Fact]
		public async Task NotingTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			Money denomination = Money.Coins(1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 1;
			bool doesNoteBeforeBan = true;
			CoordinatorRoundConfig roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 1, 1, 1, 24, doesNoteBeforeBan, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);

			var registerRequests = new List<(BitcoinWitPubKeyAddress changeOutputAddress, uint256 blindedData, InputProofModel[] inputsProofs)>();
			AliceClient aliceClientBackup = null;
			CoordinatorRound round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			for (int i = 0; i < roundConfig.AnonymitySet; i++)
			{
				BitcoinWitPubKeyAddress activeOutputAddress = new Key().PubKey.GetSegwitAddress(network);
				BitcoinWitPubKeyAddress changeOutputAddress = new Key().PubKey.GetSegwitAddress(network);
				Key inputKey = new Key();
				BitcoinWitPubKeyAddress inputAddress = inputKey.PubKey.GetSegwitAddress(network);

				var requester = new Requester();
				uint256 blinded = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, activeOutputAddress.ScriptPubKey);
				uint256 blindedOutputScriptsHash = new uint256(Hashes.SHA256(blinded.ToBytes()));

				uint256 txHash = await rpc.SendToAddressAsync(inputAddress, Money.Coins(2));
				await rpc.GenerateAsync(1);
				Transaction transaction = await rpc.GetRawTransactionAsync(txHash);
				Coin coin = transaction.Outputs.GetCoins(inputAddress.ScriptPubKey).Single();
				OutPoint input = coin.Outpoint;

				InputProofModel inputProof = new InputProofModel { Input = input, Proof = inputKey.SignCompact(blindedOutputScriptsHash) };
				InputProofModel[] inputsProofs = new InputProofModel[] { inputProof };
				registerRequests.Add((changeOutputAddress, blinded, inputsProofs));
				aliceClientBackup = await AliceClientBase.CreateNewAsync(round.RoundId, new[] { activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey }, new[] { requester }, network, changeOutputAddress, new[] { blinded }, inputsProofs, () => baseUri, null);
			}

			await WaitForTimeoutAsync(baseUri);

			int bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.Equal(0, bannedCount);
			int notedCount = coordinator.UtxoReferee.CountBanned(true);
			Assert.Equal(anonymitySet, notedCount);

			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();

			foreach (var registerRequest in registerRequests)
			{
				await AliceClientBase.CreateNewAsync(round.RoundId, aliceClientBackup.RegisteredAddresses, round.MixingLevels.GetAllLevels().Select(x => x.SchnorrKey.SchnorrPubKey), aliceClientBackup.Requesters, network, registerRequest.changeOutputAddress, new[] { registerRequest.blindedData }, registerRequest.inputsProofs, () => baseUri, null);
			}

			await WaitForTimeoutAsync(baseUri);

			bannedCount = coordinator.UtxoReferee.CountBanned(false);
			Assert.Equal(anonymitySet, bannedCount);
			notedCount = coordinator.UtxoReferee.CountBanned(true);
			Assert.Equal(anonymitySet, notedCount);
		}

#pragma warning restore IDE0059 // Value assigned to symbol is never used
	}
}

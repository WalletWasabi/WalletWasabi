using NBitcoin;
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
using WalletWasabi.CoinJoin.Common.Crypto;
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

namespace WalletWasabi.Tests.RegressionTests
{
	[Collection("RegTest collection")]
	public class CoinJoinTests
	{
#pragma warning disable IDE0059 // Value assigned to symbol is never used

		public CoinJoinTests(RegTestFixture regTestFixture)
		{
			RegTestFixture = regTestFixture;
			BaseUri = new Uri(RegTestFixture.BackendEndPoint);
		}

		private RegTestFixture RegTestFixture { get; }
		public Uri BaseUri { get; }

		[Fact]
		public async Task CoordinatorCtorTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			Logger.TurnOff(); // turn off at the end, otherwise, the tests logs would have of warnings

			var bestBlockHash = await rpc.GetBestBlockHashAsync();
			var bestBlock = await rpc.GetBlockAsync(bestBlockHash);
			var coinbaseTxId = bestBlock.Transactions[0].GetHash();
			var offchainTxId = network.Consensus.ConsensusFactory.CreateTransaction().GetHash();
			var mempoolTxId = await rpc.SendToAddressAsync(new Key().PubKey.GetSegwitAddress(network), Money.Coins(1));

			var folder = Common.GetWorkDir();
			await IoHelpers.DeleteRecursivelyWithMagicDustAsync(folder);
			Directory.CreateDirectory(folder);
			var cjfile = Path.Combine(folder, $"CoinJoins{network}.txt");
			File.WriteAllLines(cjfile, new[] { coinbaseTxId.ToString(), offchainTxId.ToString(), mempoolTxId.ToString() });

			using (var coordinatorToTest = new Coordinator(network, global.HostedServices.FirstOrDefault<BlockNotifier>(), folder, rpc, coordinator.RoundConfig))
			{
				var txIds = await File.ReadAllLinesAsync(cjfile);

				Assert.Contains(coinbaseTxId.ToString(), txIds);
				Assert.Contains(mempoolTxId.ToString(), txIds);
				Assert.DoesNotContain(offchainTxId.ToString(), txIds);

				await IoHelpers.DeleteRecursivelyWithMagicDustAsync(folder);
				Directory.CreateDirectory(folder);
				File.WriteAllLines(cjfile, new[] { coinbaseTxId.ToString(), "This line is invalid (the file is corrupted)", offchainTxId.ToString() });

				var coordinatorToTest2 = new Coordinator(network, global.HostedServices.FirstOrDefault<BlockNotifier>(), folder, rpc, coordinatorToTest.RoundConfig);
				coordinatorToTest2?.Dispose();
				txIds = await File.ReadAllLinesAsync(cjfile);
				Assert.Single(txIds);
				Assert.Contains(coinbaseTxId.ToString(), txIds);
				Assert.DoesNotContain(offchainTxId.ToString(), txIds);
				Assert.DoesNotContain("This line is invalid (the file is corrupted)", txIds);
			}

			Logger.TurnOn();
		}

		[Fact]
		public async Task CcjTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			Money denomination = Money.Coins(0.2m);
			decimal coordinatorFeePercent = 0.2m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 50;
			var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 2, 0.7, coordinatorFeePercent, anonymitySet, 100, connectionConfirmationTimeout, 50, 50, 2, 24, false, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");

			using var torClient = new TorHttpClient(BaseUri, Global.Instance.TorSocks5Endpoint);
			using var satoshiClient = new SatoshiClient(BaseUri, null);

			#region PostInputsGetStates

			// <-------------------------->
			// POST INPUTS and GET STATES tests
			// <-------------------------->

			var states = await satoshiClient.GetAllRoundStatesAsync();
			Assert.Equal(2, states.Count());
			foreach (var rs in states)
			{
				// Never changes.
				Assert.True(0 < rs.RoundId);
				Assert.Equal(Money.Coins(0.00009648m), rs.FeePerInputs);
				Assert.Equal(Money.Coins(0.00004752m), rs.FeePerOutputs);
				Assert.Equal(7, rs.MaximumInputCountPerPeer);
				// Changes per rounds.
				Assert.Equal(denomination, rs.Denomination);
				Assert.Equal(coordinatorFeePercent, rs.CoordinatorFeePercent);
				Assert.Equal(anonymitySet, rs.RequiredPeerCount);
				Assert.Equal(connectionConfirmationTimeout, rs.RegistrationTimeout);
				// Changes per phases.
				Assert.Equal(RoundPhase.InputRegistration, rs.Phase);
				Assert.Equal(0, rs.RegisteredPeerCount);
			}

			// Inputs request tests
			var inputsRequest = new InputsRequest
			{
				BlindedOutputScripts = null,
				ChangeOutputAddress = null,
				Inputs = null,
			};

			var round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			var roundId = round.RoundId;
			inputsRequest.RoundId = roundId;
			var registeredAddresses = Array.Empty<BitcoinAddress>();
			var schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
			var requesters = Array.Empty<Requester>();

			HttpRequestException httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nInvalid request.", httpRequestException.Message);

			byte[] dummySignature = new byte[65];

			inputsRequest.BlindedOutputScripts = Enumerable.Range(0, round.MixingLevels.Count() + 1).Select(x => uint256.One);
			inputsRequest.ChangeOutputAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(uint256.One, 0), Proof = dummySignature } };
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nToo many blinded output was provided", httpRequestException.Message);

			inputsRequest.BlindedOutputScripts = Enumerable.Range(0, round.MixingLevels.Count() - 2).Select(x => uint256.One);
			inputsRequest.ChangeOutputAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(uint256.One, 0), Proof = dummySignature } };
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nDuplicate blinded output found", httpRequestException.Message);

			inputsRequest.BlindedOutputScripts = new[] { uint256.Zero };
			inputsRequest.ChangeOutputAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(uint256.One, 0), Proof = dummySignature } };
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is not unspent", httpRequestException.Message);

			var addr = await rpc.GetNewAddressAsync();
			var hash = await rpc.SendToAddressAsync(addr, Money.Coins(0.01m));
			var tx = await rpc.GetRawTransactionAsync(hash);
			var coin = tx.Outputs.GetCoins(addr.ScriptPubKey).Single();

			inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = coin.Outpoint, Proof = dummySignature } };
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is neither confirmed, nor is from an unconfirmed coinjoin.", httpRequestException.Message);

			var blocks = await rpc.GenerateAsync(1);
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input must be witness_v0_keyhash.", httpRequestException.Message);

			var blockHash = blocks.Single();
			var block = await rpc.GetBlockAsync(blockHash);
			var coinbase = block.Transactions.First();
			inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(coinbase.GetHash(), 0), Proof = dummySignature } };
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is immature.", httpRequestException.Message);

			var key = new Key();
			var witnessAddress = key.PubKey.GetSegwitAddress(network);
			hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
			await rpc.GenerateAsync(1);
			tx = await rpc.GetRawTransactionAsync(hash);
			coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
			inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = coin.Outpoint, Proof = dummySignature } };
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}", httpRequestException.Message);

			var proof = key.SignCompact(uint256.One);
			inputsRequest.Inputs.First().Proof = proof;
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided proof is invalid.", httpRequestException.Message);

			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			var requester = new Requester();
			uint256 msg = new uint256(NBitcoin.Crypto.Hashes.SHA256(network.Consensus.ConsensusFactory.CreateTransaction().ToBytes()));
			uint256 blindedData = requester.BlindMessage(msg, round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey);
			inputsRequest.BlindedOutputScripts = new[] { blindedData };
			uint256 blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.ToBytes()));

			proof = key.SignCompact(blindedOutputScriptsHash);
			inputsRequest.Inputs.First().Proof = proof;
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

			roundConfig.Denomination = Money.Coins(0.01m); // exactly the same as our output
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			roundId = round.RoundId;
			inputsRequest.RoundId = roundId;
			schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

			roundConfig.Denomination = Money.Coins(0.00999999m); // one satoshi less than our output
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			roundId = round.RoundId;
			inputsRequest.RoundId = roundId;
			schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

			roundConfig.Denomination = Money.Coins(0.008m); // one satoshi less than our output
			roundConfig.ConnectionConfirmationTimeout = 7;
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			roundId = round.RoundId;
			inputsRequest.RoundId = roundId;
			schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
			requester = new Requester();
			requesters = new[] { requester };
			msg = network.Consensus.ConsensusFactory.CreateTransaction().GetHash();
			blindedData = requester.BlindMessage(msg, round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey);
			inputsRequest.BlindedOutputScripts = new[] { blindedData };
			blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.ToBytes()));
			proof = key.SignCompact(blindedOutputScriptsHash);
			inputsRequest.Inputs.First().Proof = proof;
			using (var aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest))
			{
				// Test DelayedClientRoundRegistration logic.
				ClientRoundRegistration first = null;
				var second = new ClientRoundRegistration(aliceClient,
					new[] { new SmartCoin(uint256.One, 1, Script.Empty, Money.Zero, new[] { new OutPoint(uint256.One, 1) }, Height.Unknown, true, 2) },
					BitcoinAddress.Create("12Rty3c8j3QiZSwLVaBtch6XUMZaja3RC7", Network.Main));
				first = second;
				second = null;
				Assert.NotNull(first);
				Assert.Null(second);

				Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
				Assert.True(aliceClient.RoundId > 0);

				var roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
				Assert.Equal(RoundPhase.InputRegistration, roundState.Phase);
				Assert.Equal(1, roundState.RegisteredPeerCount);

				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
				Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nBlinded output has already been registered.", httpRequestException.Message);

				roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
				Assert.Equal(RoundPhase.InputRegistration, roundState.Phase);
				Assert.Equal(1, roundState.RegisteredPeerCount);

				roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
				Assert.Equal(RoundPhase.InputRegistration, roundState.Phase);
				Assert.Equal(1, roundState.RegisteredPeerCount);
				await aliceClient.PostUnConfirmationAsync();
			}

			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			requester = new Requester();
			blindedData = requester.BlindScript(round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey.SignerPubKey, round.MixingLevels.GetBaseLevel().SchnorrKey.SchnorrPubKey.RpubKey, key.ScriptPubKey);
			inputsRequest.BlindedOutputScripts = new[] { blindedData };
			blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.ToBytes()));
			proof = key.SignCompact(blindedOutputScriptsHash);
			inputsRequest.Inputs.First().Proof = proof;

			var currentRound = coordinator.TryGetRound(roundId);
			Assert.NotNull(currentRound);
			Assert.Equal(RoundPhase.InputRegistration, currentRound.Phase);
			Assert.Equal(2, currentRound.AnonymitySet);
			Assert.Equal(0, currentRound.CountAlices());

			using (var aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest))
			{
				Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
				Assert.True(aliceClient.RoundId > 0);

				Assert.Equal(2, currentRound.AnonymitySet);
				Assert.Equal(1, currentRound.CountAlices());

				var roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
				Assert.Equal(RoundPhase.InputRegistration, roundState.Phase);
				Assert.Equal(1, roundState.RegisteredPeerCount);
			}

			inputsRequest.BlindedOutputScripts = new[] { uint256.One };
			blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(uint256.One.ToBytes()));
			proof = key.SignCompact(blindedOutputScriptsHash);
			inputsRequest.Inputs.First().Proof = proof;
			inputsRequest.Inputs = new List<InputProofModel> { inputsRequest.Inputs.First(), inputsRequest.Inputs.First() };
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nCannot register an input twice.", httpRequestException.Message);

			var inputProofs = new List<InputProofModel>();
			for (int j = 0; j < 8; j++)
			{
				key = new Key();
				witnessAddress = key.PubKey.GetSegwitAddress(network);
				hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
				await rpc.GenerateAsync(1);
				tx = await rpc.GetRawTransactionAsync(hash);
				coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
				proof = key.SignCompact(blindedOutputScriptsHash);
				inputProofs.Add(new InputProofModel { Input = coin.Outpoint, Proof = proof });
			}
			var blockHashed = await rpc.GenerateAsync(1);

			inputsRequest.Inputs = inputProofs;
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nMaximum 7 inputs can be registered.", httpRequestException.Message);
			inputProofs.RemoveLast();
			inputsRequest.Inputs = inputProofs;

			Assert.NotNull(currentRound);
			Assert.Equal(RoundPhase.InputRegistration, currentRound.Phase);
			Assert.Equal(2, currentRound.AnonymitySet);
			Assert.Equal(1, currentRound.CountAlices());

			var awaiter = new EventAwaiter<RoundPhase>(
				h => currentRound.PhaseChanged += h,
				h => currentRound.PhaseChanged -= h);

			using (var aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest))
			{
				Assert.Equal(2, currentRound.AnonymitySet);
				Assert.Equal(2, currentRound.CountAlices());
				Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
				Assert.True(aliceClient.RoundId > 0);

				await awaiter.WaitAsync(TimeSpan.FromSeconds(7));

				var roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
				Assert.Equal(RoundPhase.ConnectionConfirmation, roundState.Phase);
				Assert.Equal(2, roundState.RegisteredPeerCount);
				var inputRegistrableRoundState = await satoshiClient.GetRegistrableRoundStateAsync();
				Assert.Equal(0, inputRegistrableRoundState.RegisteredPeerCount);

				roundConfig.ConnectionConfirmationTimeout = 1; // One second.
				coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
				coordinator.AbortAllRoundsInInputRegistration("");
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				roundId = round.RoundId;
				inputsRequest.RoundId = roundId;
				schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;

				roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
				Assert.Equal(RoundPhase.ConnectionConfirmation, roundState.Phase);
				Assert.Equal(2, roundState.RegisteredPeerCount);
			}

			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nInput is already registered in another round.", httpRequestException.Message);

			// Wait until input registration times out.
			await Task.Delay(TimeSpan.FromSeconds(8));
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, inputsRequest));
			Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nInput is banned from participation for", httpRequestException.Message);

			var spendingTx = network.Consensus.ConsensusFactory.CreateTransaction();
			var bannedCoin = inputsRequest.Inputs.First().Input;
			var utxos = coordinator.UtxoReferee;
			Assert.NotNull(await utxos.TryGetBannedAsync(bannedCoin, false));
			spendingTx.Inputs.Add(new TxIn(bannedCoin));
			spendingTx.Outputs.Add(new TxOut(Money.Coins(1), new Key().PubKey.GetSegwitAddress(network)));
			await coordinator.ProcessConfirmedTransactionAsync(spendingTx);

			Assert.NotNull(await utxos.TryGetBannedAsync(new OutPoint(spendingTx.GetHash(), 0), false));
			Assert.Null(await utxos.TryGetBannedAsync(bannedCoin, false));

			states = await satoshiClient.GetAllRoundStatesAsync();
			foreach (var rs in states.Where(x => x.Phase == RoundPhase.InputRegistration))
			{
				Assert.Equal(0, rs.RegisteredPeerCount);
			}

			#endregion PostInputsGetStates

			#region PostConfirmationPostUnconfirmation

			// <-------------------------->
			// POST CONFIRMATION and POST UNCONFIRMATION tests
			// <-------------------------->

			key = new Key();
			witnessAddress = key.PubKey.GetSegwitAddress(network);
			hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
			await rpc.GenerateAsync(1);
			tx = await rpc.GetRawTransactionAsync(hash);
			coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			requester = new Requester();
			requesters = new[] { requester };
			BitcoinWitPubKeyAddress bitcoinWitPubKeyAddress = new Key().PubKey.GetSegwitAddress(network);
			registeredAddresses = new[] { bitcoinWitPubKeyAddress };
			Script script = bitcoinWitPubKeyAddress.ScriptPubKey;
			blindedData = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, script);
			blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.ToBytes()));

			using (var aliceClient = await AliceClientBase.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network), new[] { blindedData }, new InputProofModel[] { new InputProofModel { Input = coin.Outpoint, Proof = key.SignCompact(blindedOutputScriptsHash) } }, () => BaseUri, null))
			{
				Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
				Assert.True(aliceClient.RoundId > 0);
				// Double the request.
				// badrequests
				using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation"))
				{
					Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				}
				using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={aliceClient.UniqueId}"))
				{
					Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				}
				using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?roundId={aliceClient.RoundId}"))
				{
					Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				}
				using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId=foo&roundId={aliceClient.RoundId}"))
				{
					Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
					Assert.Equal("Invalid uniqueId provided.", await response.Content.ReadAsJsonAsync<string>());
				}
				using (var response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={aliceClient.UniqueId}&roundId=bar"))
				{
					Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
					Assert.Null(await response.Content.ReadAsJsonAsync<string>());
				}

				roundConfig.ConnectionConfirmationTimeout = 60;
				coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
				coordinator.AbortAllRoundsInInputRegistration("");
				round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				roundId = round.RoundId;
				inputsRequest.RoundId = roundId;
				schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await aliceClient.PostConfirmationAsync());
				Assert.Equal($"{HttpStatusCode.Gone.ToReasonString()}\nRound is not running.", httpRequestException.Message);
			}

			using (var aliceClient = await AliceClientBase.CreateNewAsync(roundId, registeredAddresses, schnorrPubKeys, requesters, network, new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network), new[] { blindedData }, new InputProofModel[] { new InputProofModel { Input = coin.Outpoint, Proof = key.SignCompact(blindedOutputScriptsHash) } }, () => BaseUri, null))
			{
				Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
				Assert.True(aliceClient.RoundId > 0);
				await aliceClient.PostUnConfirmationAsync();
				using HttpResponseMessage response = await torClient.SendAsync(HttpMethod.Post, $"/api/v{Constants.BackendMajorVersion}/btc/chaumiancoinjoin/unconfirmation?uniqueId={aliceClient.UniqueId}&roundId={aliceClient.RoundId}");
				Assert.True(response.IsSuccessStatusCode);
				Assert.Equal(HttpStatusCode.OK, response.StatusCode);
				Assert.Equal("Alice not found.", await response.Content.ReadAsJsonAsync<string>());
			}

			#endregion PostConfirmationPostUnconfirmation

			#region PostOutput

			// <-------------------------->
			// POST OUTPUT tests
			// <-------------------------->

			var key1 = new Key();
			var key2 = new Key();
			var outputAddress1 = key1.PubKey.GetSegwitAddress(network);
			var outputAddress2 = key2.PubKey.GetSegwitAddress(network);
			var hash1 = await rpc.SendToAddressAsync(outputAddress1, Money.Coins(0.01m));
			var hash2 = await rpc.SendToAddressAsync(outputAddress2, Money.Coins(0.01m));
			await rpc.GenerateAsync(1);
			var tx1 = await rpc.GetRawTransactionAsync(hash1);
			var tx2 = await rpc.GetRawTransactionAsync(hash2);
			var index1 = 0;
			for (int i = 0; i < tx1.Outputs.Count; i++)
			{
				var output = tx1.Outputs[i];
				if (output.ScriptPubKey == outputAddress1.ScriptPubKey)
				{
					index1 = i;
				}
			}
			var index2 = 0;
			for (int i = 0; i < tx2.Outputs.Count; i++)
			{
				var output = tx2.Outputs[i];
				if (output.ScriptPubKey == outputAddress2.ScriptPubKey)
				{
					index2 = i;
				}
			}

			round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			schnorrPubKeys = round.MixingLevels.SchnorrPubKeys;
			roundId = round.RoundId;

			var requester1 = new Requester();
			var requester2 = new Requester();

			uint256 blinded1 = requester1.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, outputAddress1.ScriptPubKey);
			uint256 blindedOutputScriptsHash1 = new uint256(NBitcoin.Crypto.Hashes.SHA256(blinded1.ToBytes()));
			uint256 blinded2 = requester2.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, outputAddress2.ScriptPubKey);
			uint256 blindedOutputScriptsHash2 = new uint256(NBitcoin.Crypto.Hashes.SHA256(blinded2.ToBytes()));

			var input1 = new OutPoint(hash1, index1);
			var input2 = new OutPoint(hash2, index2);

			using (var aliceClient1 = await AliceClientBase.CreateNewAsync(roundId, new[] { outputAddress1 }, schnorrPubKeys, new[] { requester1 }, network, new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network), new[] { blinded1 }, new InputProofModel[] { new InputProofModel { Input = input1, Proof = key1.SignCompact(blindedOutputScriptsHash1) } }, () => BaseUri, null))
			using (var aliceClient2 = await AliceClientBase.CreateNewAsync(roundId, new[] { outputAddress2 }, schnorrPubKeys, new[] { requester2 }, network, new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network), new[] { blinded2 }, new InputProofModel[] { new InputProofModel { Input = input2, Proof = key2.SignCompact(blindedOutputScriptsHash2) } }, () => BaseUri, null))
			{
				Assert.Equal(aliceClient2.RoundId, aliceClient1.RoundId);
				Assert.NotEqual(aliceClient2.UniqueId, aliceClient1.UniqueId);

				var connConfResp = await aliceClient1.PostConfirmationAsync();
				Assert.Equal(connConfResp.currentPhase, (await aliceClient1.PostConfirmationAsync()).currentPhase); // Make sure it won't throw error for double confirming.
				var connConfResp2 = await aliceClient2.PostConfirmationAsync();

				Assert.Equal(connConfResp.currentPhase, connConfResp2.currentPhase);
				httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await aliceClient2.PostConfirmationAsync());
				Assert.Equal($"{HttpStatusCode.Gone.ToReasonString()}\nParticipation can be only confirmed from InputRegistration or ConnectionConfirmation phase. Current phase: OutputRegistration.", httpRequestException.Message);

				var roundState = await satoshiClient.GetRoundStateAsync(aliceClient1.RoundId);
				Assert.Equal(RoundPhase.OutputRegistration, roundState.Phase);

				if (!round.MixingLevels.GetBaseLevel().Signer.VerifyUnblindedSignature(connConfResp2.activeOutputs.First().Signature, outputAddress2.ScriptPubKey.ToBytes()))
				{
					throw new NotSupportedException("Coordinator did not sign the blinded output properly.");
				}

				using (var bobClient1 = new BobClient(BaseUri, null))
				using (var bobClient2 = new BobClient(BaseUri, null))
				{
					await bobClient1.PostOutputAsync(aliceClient1.RoundId, new ActiveOutput(outputAddress1, connConfResp.activeOutputs.First().Signature, 0));
					await bobClient2.PostOutputAsync(aliceClient2.RoundId, new ActiveOutput(outputAddress2, connConfResp2.activeOutputs.First().Signature, 0));
				}

				roundState = await satoshiClient.GetRoundStateAsync(aliceClient1.RoundId);
				Assert.Equal(RoundPhase.Signing, roundState.Phase);
				Assert.Equal(2, roundState.RegisteredPeerCount);
				Assert.Equal(2, roundState.RequiredPeerCount);

				#endregion PostOutput

				#region GetCoinjoin

				// <-------------------------->
				// GET COINJOIN tests
				// <-------------------------->

				Transaction unsignedCoinJoin = await aliceClient1.GetUnsignedCoinJoinAsync();
				Assert.Equal(unsignedCoinJoin.ToHex(), (await aliceClient1.GetUnsignedCoinJoinAsync()).ToHex());
				Assert.Equal(unsignedCoinJoin.ToHex(), (await aliceClient2.GetUnsignedCoinJoinAsync()).ToHex());

				Assert.Contains(outputAddress1.ScriptPubKey, unsignedCoinJoin.Outputs.Select(x => x.ScriptPubKey));
				Assert.Contains(outputAddress2.ScriptPubKey, unsignedCoinJoin.Outputs.Select(x => x.ScriptPubKey));
				Assert.True(2 == unsignedCoinJoin.Outputs.Count); // Because the two inputs are equal, so change addresses won't be used, nor coordinator fee will be taken.
				Assert.Contains(input1, unsignedCoinJoin.Inputs.Select(x => x.PrevOut));
				Assert.Contains(input2, unsignedCoinJoin.Inputs.Select(x => x.PrevOut));
				Assert.True(2 == unsignedCoinJoin.Inputs.Count);

				#endregion GetCoinjoin

				#region PostSignatures

				// <-------------------------->
				// POST SIGNATURES tests
				// <-------------------------->

				var partSignedCj1 = Transaction.Parse(unsignedCoinJoin.ToHex(), network);
				var partSignedCj2 = Transaction.Parse(unsignedCoinJoin.ToHex(), network);

				partSignedCj1.Sign(
					key1.GetBitcoinSecret(network),
					new Coin(tx1, input1.N));
				partSignedCj2.Sign(
					key2.GetBitcoinSecret(network),
					new Coin(tx2, input2.N));

				var myDic1 = new Dictionary<int, WitScript>();
				var myDic2 = new Dictionary<int, WitScript>();

				for (int i = 0; i < unsignedCoinJoin.Inputs.Count; i++)
				{
					var input = unsignedCoinJoin.Inputs[i];
					if (input.PrevOut == input1)
					{
						myDic1.Add(i, partSignedCj1.Inputs[i].WitScript);
					}
					if (input.PrevOut == input2)
					{
						myDic2.Add(i, partSignedCj2.Inputs[i].WitScript);
					}
				}

				await aliceClient1.PostSignaturesAsync(myDic1);
				await aliceClient2.PostSignaturesAsync(myDic2);

				((CachedRpcClient)rpc)?.Cache.Remove("GetRawMempoolAsync");

				uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
				Assert.Contains(unsignedCoinJoin.GetHash(), mempooltxs);

				var wasabiClient = new WasabiClient(BaseUri, null);
				var syncInfo = await wasabiClient.GetSynchronizeAsync(blockHashed[0], 1);
				Assert.Contains(unsignedCoinJoin.GetHash(), syncInfo.UnconfirmedCoinJoins);
				var txs = await wasabiClient.GetTransactionsAsync(network, new[] { unsignedCoinJoin.GetHash() }, CancellationToken.None);
				Assert.NotEmpty(txs);

				#endregion PostSignatures
			}
		}

		[Fact]
		public async Task CcjEqualInputTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.0002m;
			int anonymitySet = 4;
			int connectionConfirmationTimeout = 50;
			var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 2, 0.7, coordinatorFeePercent, anonymitySet, 100, connectionConfirmationTimeout, 50, 50, 2, 24, false, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);
			using var torClient = new TorHttpClient(baseUri, Global.Instance.TorSocks5Endpoint);
			using var satoshiClient = new SatoshiClient(baseUri, null);
			var round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
			var roundId = round.RoundId;

			// We have to 4 participant so, this data structure is for keeping track of
			// important data for each of the participants in the coinjoin session.
			var participants = new List<(AliceClient aliceClient,
										 List<(Requester requester, BitcoinWitPubKeyAddress outputAddress, uint256 blindedScript)> outouts,
										 List<(OutPoint input, byte[] proof, Coin coin, Key key)> inputs)>();

			// INPUTS REGISTRATION PHASE --
			for (var anosetIdx = 0; anosetIdx < anonymitySet; anosetIdx++)
			{
				// Create as many outputs as mixin levels (even when we do not have funds enough)
				var outputs = new List<(Requester requester, BitcoinWitPubKeyAddress outputAddress, uint256 blindedScript)>();
				foreach (var level in round.MixingLevels.Levels)
				{
					var requester = new Requester();
					var outputsAddress = new Key().PubKey.GetSegwitAddress(network);
					var scriptPubKey = outputsAddress.ScriptPubKey;
					// We blind the scriptPubKey with a new requester by mixin level
					var blindedScript = requester.BlindScript(level.Signer.Key.PubKey, level.Signer.R.PubKey, scriptPubKey);
					outputs.Add((requester, outputsAddress, blindedScript));
				}

				// Calculate the SHA256( blind1 || blind2 || .....|| blindN )
				var blindedOutputScriptList = outputs.Select(x => x.blindedScript);
				var blindedOutputScriptListBytes = ByteHelpers.Combine(blindedOutputScriptList.Select(x => x.ToBytes()));
				var blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedOutputScriptListBytes));

				// Create 4 new coins that we want to mix
				var inputs = new List<(OutPoint input, byte[] proof, Coin coin, Key key)>();
				for (var inputIdx = 0; inputIdx < 4; inputIdx++)
				{
					var key = new Key();
					var outputAddress = key.PubKey.GetSegwitAddress(network);
					var hash = await rpc.SendToAddressAsync(outputAddress, Money.Coins(0.1m));
					await rpc.GenerateAsync(1);
					var tx = await rpc.GetRawTransactionAsync(hash);
					var index = tx.Outputs.FindIndex(x => x.ScriptPubKey == outputAddress.ScriptPubKey);
					var input = new OutPoint(hash, index);

					inputs.Add((
						input,
						key.SignCompact(blindedOutputScriptsHash),
						new Coin(tx, (uint)index),
						key
					));
				}

				// Save alice client and the outputs, requesters, etc
				var changeOutput = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				var inputProof = inputs.Select(x => new InputProofModel { Input = x.input, Proof = x.proof }).ToArray();
				var aliceClient = await AliceClientBase.CreateNewAsync(
					round.RoundId,
					outputs.Select(x => x.outputAddress),
					round.MixingLevels.SchnorrPubKeys,
					outputs.Select(x => x.requester),
					network, changeOutput, blindedOutputScriptList, inputProof, () => baseUri, null);

				// We check the coordinator signed all the alice blinded outputs
				participants.Add((aliceClient, outputs, inputs));
			}

			// CONNECTION CONFIRMATION PHASE --
			var activeOutputs = new List<IEnumerable<ActiveOutput>>();
			var j = 0;
			foreach (var (aliceClient, _, _) in participants)
			{
				var res = await aliceClient.PostConfirmationAsync();
				activeOutputs.Add(res.activeOutputs);
				j++;
			}

			// OUTPUTS REGISTRATION PHASE --
			var roundState = await satoshiClient.GetRoundStateAsync(roundId);
			Assert.Equal(RoundPhase.OutputRegistration, roundState.Phase);

			var l = 0;
			foreach (var (aliceClient, outputs, _) in participants)
			{
				using (var bobClient = new BobClient(baseUri, null))
				{
					var i = 0;
					foreach (var output in outputs.Take(activeOutputs[l].Count()))
					{
						await bobClient.PostOutputAsync(aliceClient.RoundId, new ActiveOutput(output.outputAddress, activeOutputs[l].ElementAt(i).Signature, i));
						i++;
					}
				}
				l++;
			}

			// SIGNING PHASE --
			roundState = await satoshiClient.GetRoundStateAsync(roundId);
			Assert.Equal(RoundPhase.Signing, roundState.Phase);

			uint256 transactionId = null;
			foreach (var (aliceClient, outputs, inputs) in participants)
			{
				var unsignedTransaction = await aliceClient.GetUnsignedCoinJoinAsync();
				transactionId = unsignedTransaction.GetHash();

				// Verify the transaction contains the expected inputs and outputs

				// Verify the inputs are the expected ones.
				foreach (var input in inputs)
				{
					Assert.Contains(input.input, unsignedTransaction.Inputs.Select(x => x.PrevOut));
				}

				// Sign the transaction
				var partSignedCj = unsignedTransaction.Clone();
				partSignedCj.Sign(
					inputs.Select(x => x.key.GetBitcoinSecret(network)),
					inputs.Select(x => x.coin));

				var witnesses = partSignedCj.Inputs
					.AsIndexedInputs()
					.Where(x => x.WitScript != WitScript.Empty)
					.ToDictionary(x => (int)x.Index, x => x.WitScript);

				await aliceClient.PostSignaturesAsync(witnesses);
			}

			((CachedRpcClient)rpc)?.Cache.Remove("GetRawMempoolAsync");

			uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
			Assert.Contains(transactionId, mempooltxs);
		}

		[Fact]
		public async Task Ccj100ParticipantsTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.003m;
			int anonymitySet = 100;
			int connectionConfirmationTimeout = 120;
			var roundConfig = RegTestFixture.CreateRoundConfig(denomination, Constants.OneDayConfirmationTarget, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			await rpc.GenerateAsync(100); // So to make sure we have enough money.

			Uri baseUri = new Uri(RegTestFixture.BackendEndPoint);
			var spentCoins = new List<Coin>();
			var fundingTxCount = 0;
			var inputRegistrationUsers = new List<(Requester requester, uint256 blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinWitPubKeyAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData)>();
			for (int i = 0; i < roundConfig.AnonymitySet; i++)
			{
				var userInputData = new List<(Key key, BitcoinWitPubKeyAddress inputAddress, uint256 txHash, Transaction tx, OutPoint input)>();
				var activeOutputAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				var changeOutputAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				CoordinatorRound round = coordinator.GetCurrentInputRegisterableRoundOrDefault();
				var requester = new Requester();
				uint256 blinded = requester.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, round.MixingLevels.GetBaseLevel().Signer.R.PubKey, activeOutputAddress.ScriptPubKey);
				uint256 blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blinded.ToBytes()));

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
					spentCoins.Add(coin);

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

			Logger.TurnOff();

			var aliceClients = new List<Task<AliceClient>>();

			var currentRound = coordinator.GetCurrentInputRegisterableRoundOrDefault();

			foreach (var user in inputRegistrationUsers)
			{
				aliceClients.Add(AliceClientBase.CreateNewAsync(currentRound.RoundId, new[] { user.activeOutputAddress }, currentRound.MixingLevels.SchnorrPubKeys, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, () => baseUri, null));
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

			Logger.TurnOn();

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

				// Because it's valuetuple.
				var user = users.ElementAt(k);
				users.RemoveAt(k);
				users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, user.aliceClient, resp.Item2.First().Signature));
			}

			var outputRequests = new List<(BobClient, Task)>();
			foreach (var user in users)
			{
				var bobClient = new BobClient(baseUri, null);
				outputRequests.Add((bobClient, bobClient.PostOutputAsync(roundId, new ActiveOutput(user.activeOutputAddress, user.unblindedSignature, 0))));
			}

			foreach (var request in outputRequests)
			{
				await request.Item2;
				request.Item1?.Dispose();
			}

			var coinjoinRequests = new List<Task<Transaction>>();
			foreach (var user in users)
			{
				coinjoinRequests.Add(user.aliceClient.GetUnsignedCoinJoinAsync());
			}

			Transaction unsignedCoinJoin = null;
			foreach (var request in coinjoinRequests)
			{
				if (unsignedCoinJoin is null)
				{
					unsignedCoinJoin = await request;
				}
				else
				{
					Assert.Equal(unsignedCoinJoin.ToHex(), (await request).ToHex());
				}
			}

			var signatureRequests = new List<Task>();
			foreach (var user in users)
			{
				var partSignedCj = Transaction.Parse(unsignedCoinJoin.ToHex(), network);
				partSignedCj.Sign(
					user.userInputData.Select(x => x.key.GetBitcoinSecret(network)),
					user.userInputData.Select(x => new Coin(x.tx, x.input.N)));

				var myDic = new Dictionary<int, WitScript>();

				long previousAmount = -1;
				for (int i = 0; i < unsignedCoinJoin.Inputs.Count; i++)
				{
					var input = unsignedCoinJoin.Inputs[i];
					long currentAmount = spentCoins.Single(x => x.Outpoint == unsignedCoinJoin.Inputs[i].PrevOut).Amount;
					Assert.True(previousAmount <= currentAmount);
					previousAmount = currentAmount;
					if (user.userInputData.Select(x => x.input).Contains(input.PrevOut))
					{
						myDic.Add(i, partSignedCj.Inputs[i].WitScript);
					}
				}

				signatureRequests.Add(user.aliceClient.PostSignaturesAsync(myDic));
			}

			await Task.WhenAll(signatureRequests);

			((CachedRpcClient)rpc)?.Cache.Remove("GetRawMempoolAsync");
			uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
			Assert.Contains(unsignedCoinJoin.GetHash(), mempooltxs);

			var coins = new List<Coin>();
			var finalCoinjoin = await rpc.GetRawTransactionAsync(mempooltxs.First());
			foreach (var input in finalCoinjoin.Inputs)
			{
				var getTxOut = await rpc.GetTxOutAsync(input.PrevOut.Hash, (int)input.PrevOut.N, includeMempool: false);

				coins.Add(new Coin(input.PrevOut.Hash, input.PrevOut.N, getTxOut.TxOut.Value, getTxOut.TxOut.ScriptPubKey));
			}

			FeeRate feeRateTx = finalCoinjoin.GetFeeRate(coins.ToArray());
			var esr = await rpc.EstimateSmartFeeAsync(roundConfig.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true);
			FeeRate feeRateReal = esr.FeeRate;

			Assert.True(feeRateReal.FeePerK - feeRateReal.FeePerK / 2 < feeRateTx.FeePerK); // Max 50% mistake.
			Assert.True(2 * feeRateReal.FeePerK > feeRateTx.FeePerK); // Max 200% mistake.

			var activeOutput = finalCoinjoin.GetIndistinguishableOutputs(includeSingle: true).OrderByDescending(x => x.count).First();
			Assert.True(activeOutput.value >= roundConfig.Denomination);
			Assert.True(activeOutput.count >= roundConfig.AnonymitySet);

			foreach (var aliceClient in aliceClients)
			{
				aliceClient?.Dispose();
			}
		}

		[Fact]
		public async Task CcjFeeTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			var synchronizer = new WasabiSynchronizer(network, bitcoinStore, new Uri(RegTestFixture.BackendEndPoint), null);
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.

			Money denomination = Money.Coins(0.9m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 7;
			int connectionConfirmationTimeout = 14;
			var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");

			var participants = new List<dynamic>();

			// 1. Prepare and start services.
			for (int i = 0; i < anonymitySet; i++)
			{
				double damount = i switch
				{
					0 => 1,
					1 => 1.1,
					2 => 1.2,
					3 => 3.1,
					4 => 4.1,
					5 => 7.1,
					6 => 8.1,
					_ => 1
				};

				var amount = Money.Coins((decimal)damount);

				var keyManager = KeyManager.CreateNew(out _, password);
				var key = keyManager.GenerateNewKey("foo", KeyState.Clean, false);
				var bech = key.GetP2wpkhAddress(network);
				var txId = await rpc.SendToAddressAsync(bech, amount, replaceable: false);
				key.SetKeyState(KeyState.Used);
				var tx = await rpc.GetRawTransactionAsync(txId);
				var height = await rpc.GetBlockCountAsync();
				var bechCoin = tx.Outputs.GetCoins(bech.ScriptPubKey).Single();

				var smartCoin = new SmartCoin(bechCoin, tx.Inputs.ToOutPoints().ToArray(), height + 1, replaceable: false, anonymitySet: tx.GetAnonymitySet(bechCoin.Outpoint.N));

				var chaumianClient = new CoinJoinClient(synchronizer, rpc.Network, keyManager);

				participants.Add((smartCoin, chaumianClient));
			}

			await rpc.GenerateAsync(1);

			try
			{
				// 2. Start mixing.
				foreach (var participant in participants)
				{
					SmartCoin coin = participant.Item1;

					CoinJoinClient chaumianClient = participant.Item2;
					chaumianClient.Start();

					await chaumianClient.QueueCoinsToMixAsync(password, coin);
				}

				Task timeout = Task.Delay(TimeSpan.FromSeconds(connectionConfirmationTimeout * 2 + 7 * 2 + 7 * 2 + 7 * 2));
				while ((await rpc.GetRawMempoolAsync()).Length == 0)
				{
					if (timeout.IsCompletedSuccessfully)
					{
						throw new TimeoutException("CoinJoin was not propagated.");
					}

					await Task.Delay(1000);
				}
			}
			finally
			{
				foreach (var participant in participants)
				{
					SmartCoin coin = participant.Item1;
					CoinJoinClient chaumianClient = participant.Item2;

					Task timeout = Task.Delay(3000);
					while (chaumianClient.State.GetActivelyMixingRounds().Any())
					{
						if (timeout.IsCompletedSuccessfully)
						{
							throw new TimeoutException("CoinJoin was not noticed.");
						}
						await Task.Delay(1000);
					}

					if (chaumianClient != null)
					{
						await chaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.UserRequested);
						await chaumianClient.StopAsync(CancellationToken.None);
					}
				}
			}
		}

		[Fact]
		public async Task CoinJoinClientTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			var synchronizer = new WasabiSynchronizer(network, bitcoinStore, new Uri(RegTestFixture.BackendEndPoint), null);
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 14;
			var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			await rpc.GenerateAsync(3); // So to make sure we have enough money.
			var keyManager = KeyManager.CreateNew(out _, password);
			var key1 = keyManager.GenerateNewKey("foo", KeyState.Clean, false);
			var key2 = keyManager.GenerateNewKey("bar", KeyState.Clean, false);
			var key3 = keyManager.GenerateNewKey("baz", KeyState.Clean, false);
			var key4 = keyManager.GenerateNewKey("qux", KeyState.Clean, false);
			var bech1 = key1.GetP2wpkhAddress(network);
			var bech2 = key2.GetP2wpkhAddress(network);
			var bech3 = key3.GetP2wpkhAddress(network);
			var bech4 = key4.GetP2wpkhAddress(network);
			var amount1 = Money.Coins(0.03m);
			var amount2 = Money.Coins(0.08m);
			var amount3 = Money.Coins(0.3m);
			var amount4 = Money.Coins(0.4m);
			var txId1 = await rpc.SendToAddressAsync(bech1, amount1, replaceable: false);
			var txId2 = await rpc.SendToAddressAsync(bech2, amount2, replaceable: false);
			var txId3 = await rpc.SendToAddressAsync(bech3, amount3, replaceable: false);
			var txId4 = await rpc.SendToAddressAsync(bech4, amount4, replaceable: false);
			key1.SetKeyState(KeyState.Used);
			key2.SetKeyState(KeyState.Used);
			key3.SetKeyState(KeyState.Used);
			key4.SetKeyState(KeyState.Used);
			var tx1 = await rpc.GetRawTransactionAsync(txId1);
			var tx2 = await rpc.GetRawTransactionAsync(txId2);
			var tx3 = await rpc.GetRawTransactionAsync(txId3);
			var tx4 = await rpc.GetRawTransactionAsync(txId4);
			await rpc.GenerateAsync(1);
			var height = await rpc.GetBlockCountAsync();
			var bech1Coin = tx1.Outputs.GetCoins(bech1.ScriptPubKey).Single();
			var bech2Coin = tx2.Outputs.GetCoins(bech2.ScriptPubKey).Single();
			var bech3Coin = tx3.Outputs.GetCoins(bech3.ScriptPubKey).Single();
			var bech4Coin = tx4.Outputs.GetCoins(bech4.ScriptPubKey).Single();

			var smartCoin1 = new SmartCoin(bech1Coin, tx1.Inputs.ToOutPoints().ToArray(), height, replaceable: false, anonymitySet: tx1.GetAnonymitySet(bech1Coin.Outpoint.N));
			var smartCoin2 = new SmartCoin(bech2Coin, tx2.Inputs.ToOutPoints().ToArray(), height, replaceable: false, anonymitySet: tx2.GetAnonymitySet(bech2Coin.Outpoint.N));
			var smartCoin3 = new SmartCoin(bech3Coin, tx3.Inputs.ToOutPoints().ToArray(), height, replaceable: false, anonymitySet: tx3.GetAnonymitySet(bech3Coin.Outpoint.N));
			var smartCoin4 = new SmartCoin(bech4Coin, tx4.Inputs.ToOutPoints().ToArray(), height, replaceable: false, anonymitySet: tx4.GetAnonymitySet(bech4Coin.Outpoint.N));

			var chaumianClient1 = new CoinJoinClient(synchronizer, rpc.Network, keyManager);
			var chaumianClient2 = new CoinJoinClient(synchronizer, rpc.Network, keyManager);
			try
			{
				chaumianClient1.Start(); // Exactly delay it for 2 seconds, this will make sure of timeout later.
				chaumianClient2.Start();

				smartCoin1.CoinJoinInProgress = true;
				Assert.False((await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1)).Any()); // Inconsistent internal state, so do not try to add.
				Assert.True(smartCoin1.CoinJoinInProgress);

				await Assert.ThrowsAsync<SecurityException>(async () => await chaumianClient1.QueueCoinsToMixAsync("asdasdasd", smartCoin1, smartCoin2));
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.False(smartCoin2.CoinJoinInProgress);
				smartCoin1.CoinJoinInProgress = false;

				await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2);
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.True(smartCoin2.CoinJoinInProgress);

				// Make sure it does not throw.
				await chaumianClient1.DequeueCoinsFromMixAsync(new SmartCoin(network.Consensus.ConsensusFactory.CreateTransaction().GetHash(), 1, new Script(), Money.Parse("3"), new[] { new OutPoint(uint256.One, 0) }, Height.Mempool, replaceable: false, anonymitySet: 1), DequeueReason.UserRequested);

				Assert.True(2 == (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
				await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin1, DequeueReason.UserRequested);
				Assert.False(smartCoin1.CoinJoinInProgress);
				await chaumianClient1.DequeueCoinsFromMixAsync(new[] { smartCoin1, smartCoin2 }, DequeueReason.UserRequested);
				Assert.False(smartCoin1.CoinJoinInProgress);
				Assert.False(smartCoin2.CoinJoinInProgress);
				Assert.True(2 == (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.True(smartCoin2.CoinJoinInProgress);
				await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin1, DequeueReason.UserRequested);
				await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin2, DequeueReason.UserRequested);
				Assert.False(smartCoin1.CoinJoinInProgress);
				Assert.False(smartCoin2.CoinJoinInProgress);

				Assert.True(2 == (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
				Assert.True(smartCoin1.CoinJoinInProgress);
				Assert.True(smartCoin2.CoinJoinInProgress);
				Assert.True(1 == (await chaumianClient2.QueueCoinsToMixAsync(password, smartCoin3)).Count());

				Task timeout = Task.Delay(TimeSpan.FromSeconds(connectionConfirmationTimeout * 2 + 7 * 2 + 7 * 2 + 7 * 2));
				while ((await rpc.GetRawMempoolAsync()).Length == 0)
				{
					if (timeout.IsCompletedSuccessfully)
					{
						throw new TimeoutException("CoinJoin was not propagated.");
					}
					await Task.Delay(1000);
				}

				var cj = (await rpc.GetRawMempoolAsync()).Single();
				smartCoin1.SpenderTransactionId = cj;
				smartCoin2.SpenderTransactionId = cj;
				smartCoin3.SpenderTransactionId = cj;

				// Make sure if times out, it tries again.
				connectionConfirmationTimeout = 1;
				roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
				coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
				coordinator.AbortAllRoundsInInputRegistration("");
				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				await chaumianClient1.DequeueAllCoinsFromMixAsync(DequeueReason.UserRequested);
				Assert.Empty(chaumianClient1.State.GetAllQueuedCoins());
				await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin4);
				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				Assert.NotEmpty(chaumianClient1.State.GetAllWaitingCoins());
				Assert.Empty(chaumianClient1.State.GetAllRegisteredCoins());
				while (chaumianClient1.State.GetAllWaitingCoins().Any())
				{
					await Task.Delay(1000);
				}
				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				Assert.Empty(chaumianClient1.State.GetAllWaitingCoins());
				Assert.NotEmpty(chaumianClient1.State.GetAllRegisteredCoins());
				int times = 0;
				while (!chaumianClient1.State.GetAllWaitingCoins().Any()) // // Make sure to wait until times out.
				{
					await Task.Delay(1000);
					if (times > 21)
					{
						throw new TimeoutException($"{nameof(chaumianClient1.State)}.{nameof(chaumianClient1.State.GetAllWaitingCoins)}() always empty.");
					}
					times++;
				}

				Assert.NotEmpty(chaumianClient1.State.GetAllQueuedCoins());
				Assert.Empty(chaumianClient1.State.GetAllRegisteredCoins());
			}
			finally
			{
				if (chaumianClient1 != null)
				{
					await chaumianClient1.StopAsync(CancellationToken.None);
				}
				if (chaumianClient2 != null)
				{
					await chaumianClient2.StopAsync(CancellationToken.None);
				}
			}
		}

		[Fact]
		public async Task CoinJoinMultipleRoundTestsAsync()
		{
			(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 3);

			Money denomination = Money.Coins(0.1m);
			decimal coordinatorFeePercent = 0.1m;
			int anonymitySet = 2;
			int connectionConfirmationTimeout = 14;
			var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");

			// Create the services.
			// 1. Create connection service.
			var nodes = new NodesGroup(global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

			var nodes2 = new NodesGroup(global.Config.Network, requirements: Constants.NodeRequirements);
			nodes2.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

			// 2. Create mempool service.

			Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
			node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

			Node node2 = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
			node2.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

			// 3. Create wasabi synchronizer service.
			var synchronizer = new WasabiSynchronizer(network, bitcoinStore, new Uri(RegTestFixture.BackendEndPoint), null);

			var indexFilePath2 = Path.Combine(Common.GetWorkDir(), $"Index{network}2.dat");
			var synchronizer2 = new WasabiSynchronizer(network, bitcoinStore, new Uri(RegTestFixture.BackendEndPoint), null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password);

			var keyManager2 = KeyManager.CreateNew(out _, password);

			// 5. Create wallet service.
			var workDir = Common.GetWorkDir();

			CachedBlockProvider blockProvider = new CachedBlockProvider(
				new P2pBlockProvider(nodes, null, synchronizer, serviceConfiguration, network),
				bitcoinStore.BlockRepository);

			CachedBlockProvider blockProvider2 = new CachedBlockProvider(
				new P2pBlockProvider(nodes2, null, synchronizer, serviceConfiguration, network),
				bitcoinStore.BlockRepository);

			using var wallet = Wallet.CreateAndRegisterServices(network, bitcoinStore, keyManager, synchronizer, nodes, workDir, serviceConfiguration, synchronizer, blockProvider);
			wallet.NewFilterProcessed += Common.Wallet_NewFilterProcessed;

			var workDir2 = Path.Combine(Common.GetWorkDir(), "2");
			using var wallet2 = Wallet.CreateAndRegisterServices(network, bitcoinStore, keyManager2, synchronizer2, nodes2, workDir2, serviceConfiguration, synchronizer2, blockProvider2);

			// Get some money, make it confirm.
			var key = keyManager.GetNextReceiveKey("fundZeroLink", out _);
			var txId = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(1m));
			Assert.NotNull(txId);
			var key2 = keyManager2.GetNextReceiveKey("fundZeroLink", out _);
			var key3 = keyManager2.GetNextReceiveKey("fundZeroLink", out _);
			var key4 = keyManager2.GetNextReceiveKey("fundZeroLink", out _);
			var txId2 = await rpc.SendToAddressAsync(key2.GetP2wpkhAddress(network), Money.Coins(0.11m));
			var txId3 = await rpc.SendToAddressAsync(key3.GetP2wpkhAddress(network), Money.Coins(0.12m));
			var txId4 = await rpc.SendToAddressAsync(key4.GetP2wpkhAddress(network), Money.Coins(0.13m));

			await rpc.GenerateAsync(1);

			try
			{
				Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
				nodes.Connect(); // Start connection service.
				node.VersionHandshake(); // Start mempool service.
				synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.

				nodes2.Connect(); // Start connection service.
				node2.VersionHandshake(); // Start mempool service.
				synchronizer2.Start(requestInterval: TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), 10000); // Start wasabi synchronizer service.

				// Wait until the filter our previous transaction is present.
				var blockCount = await rpc.GetBlockCountAsync();
				await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);

				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet.StartAsync(cts.Token); // Initialize wallet service.
				}
				using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
				{
					await wallet2.StartAsync(cts.Token); // Initialize wallet service.
				}

				var waitCount = 0;
				while (wallet.Coins.Sum(x => x.Amount) == Money.Zero)
				{
					await Task.Delay(1000);
					waitCount++;
					if (waitCount >= 21)
					{
						throw new TimeoutException("Funding transaction to the wallet1 did not arrive.");
					}
				}
				waitCount = 0;
				while (wallet2.Coins.Sum(x => x.Amount) == Money.Zero)
				{
					await Task.Delay(1000);
					waitCount++;
					if (waitCount >= 21)
					{
						throw new TimeoutException("Funding transaction to the wallet2 did not arrive.");
					}
				}

				Assert.True(1 == (await wallet.ChaumianClient.QueueCoinsToMixAsync(password, wallet.Coins.ToArray())).Count());
				Assert.True(3 == (await wallet2.ChaumianClient.QueueCoinsToMixAsync(password, wallet2.Coins.ToArray())).Count());

				Task timeout = Task.Delay(TimeSpan.FromSeconds(2 * (1 + 11 + 7 + 3 * (3 + 7))));
				while (wallet.Coins.Count() != 4)
				{
					if (timeout.IsCompletedSuccessfully)
					{
						throw new TimeoutException("CoinJoin was not propagated or did not arrive.");
					}
					await Task.Delay(1000);
				}

				var times = 0;
				while (wallet.Coins.FirstOrDefault(x => x.Label.IsEmpty) is null)
				{
					await Task.Delay(1000);
					times++;
					if (times >= 21)
					{
						throw new TimeoutException("Wallet spends were not recognized.");
					}
				}

				DateTime start = DateTime.Now;
				do
				{
					try
					{
						await wallet.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.UserRequested);
						await wallet2.ChaumianClient.DequeueAllCoinsFromMixAsync(DequeueReason.UserRequested);
						break;
					}
					catch (NotSupportedException)
					{
						await Task.Delay(1000);
					}

					if (DateTime.Now - start > TimeSpan.FromMinutes(1))
					{
						throw new TimeoutException("Dequeuing timed out.");
					}
				}
				while (true);

				var allCoins = wallet.TransactionProcessor.Coins.AsAllCoinsView().ToArray();
				var allCoins2 = wallet2.TransactionProcessor.Coins.AsAllCoinsView().ToArray();

				Assert.Equal(4, allCoins.Count(x => x.Label.IsEmpty && !x.Unavailable));
				Assert.Equal(3, allCoins2.Count(x => x.Label.IsEmpty && !x.Unavailable));
				Assert.Equal(2, allCoins.Count(x => x.Label.IsEmpty && !x.Unspent));
				Assert.Equal(0, allCoins2.Count(x => x.Label.IsEmpty && !x.Unspent));
				Assert.Equal(3, allCoins2.Count(x => x.Label.IsEmpty));
				Assert.Equal(4, allCoins.Count(x => x.Label.IsEmpty && x.Unspent));
			}
			finally
			{
				wallet.NewFilterProcessed -= Common.Wallet_NewFilterProcessed;
				await wallet.StopAsync(CancellationToken.None);
				// Dispose connection service.
				nodes?.Dispose();
				// Dispose mempool serving node.
				node?.Disconnect();
				await wallet2.StopAsync(CancellationToken.None);
				// Dispose wasabi synchronizer service.
				if (synchronizer is { })
				{
					await synchronizer.StopAsync();
				}
				// Dispose connection service.
				nodes2?.Dispose();
			}
		}

#pragma warning restore IDE0059 // Value assigned to symbol is never used

		private async Task<AliceClient> CreateNewAliceClientAsync(
			long roundId,
			IEnumerable<BitcoinAddress> registeredAddresses,
			IEnumerable<SchnorrPubKey> schnorrPubKeys,
			IEnumerable<Requester> requesters,
			InputsRequest request)
		{
			return await AliceClientBase.CreateNewAsync(
				roundId,
				registeredAddresses,
				schnorrPubKeys,
				requesters,
				Network.RegTest,
				request.ChangeOutputAddress,
				request.BlindedOutputScripts,
				request.Inputs,
				() => BaseUri,
				null).ConfigureAwait(false);
		}
	}
}

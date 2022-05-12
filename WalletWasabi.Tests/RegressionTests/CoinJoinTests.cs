using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Crypto;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using static WalletWasabi.Crypto.SchnorrBlinding;

namespace WalletWasabi.Tests.RegressionTests;

[Collection("RegTest collection")]
public class CoinJoinTests
{
	public CoinJoinTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
		BaseUri = new Uri(RegTestFixture.BackendEndPoint);
		BackendClearnetHttpClient = RegTestFixture.BackendHttpClient;
	}

	private RegTestFixture RegTestFixture { get; }
	public Uri BaseUri { get; }
	public ClearnetHttpClient BackendClearnetHttpClient { get; }

	[Fact]
	public async Task CoordinatorCtorTestsAsync()
	{
		(_, IRPCClient rpc, Network network, Coordinator coordinator, _, _, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		Logger.TurnOff(); // turn off at the end, otherwise, the tests logs would have of warnings

		var bestBlockHash = await rpc.GetBestBlockHashAsync();
		var bestBlock = await rpc.GetBlockAsync(bestBlockHash);
		var coinbaseTxId = bestBlock.Transactions[0].GetHash();
		var offchainTxId = network.Consensus.ConsensusFactory.CreateTransaction().GetHash();
		using var key = new Key();
		var mempoolTxId = await rpc.SendToAddressAsync(key.PubKey.GetAddress(ScriptPubKeyType.Segwit, network), Money.Coins(1));

		var folder = Helpers.Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(folder);
		Directory.CreateDirectory(folder);
		var cjfile = Path.Combine(folder, $"CoinJoins{network}.txt");
		File.WriteAllLines(cjfile, new[] { coinbaseTxId.ToString(), offchainTxId.ToString(), mempoolTxId.ToString() });

		using (var coordinatorToTest = new Coordinator(network, global.HostedServices.Get<BlockNotifier>(), folder, rpc, coordinator.RoundConfig))
		{
			var txIds = await File.ReadAllLinesAsync(cjfile);

			Assert.Contains(coinbaseTxId.ToString(), txIds);
			Assert.Contains(mempoolTxId.ToString(), txIds);
			Assert.DoesNotContain(offchainTxId.ToString(), txIds);

			await IoHelpers.TryDeleteDirectoryAsync(folder);
			Directory.CreateDirectory(folder);
			File.WriteAllLines(cjfile, new[] { coinbaseTxId.ToString(), "This line is invalid (the file is corrupted)", offchainTxId.ToString() });

			Coordinator coordinatorToTest2 = new(network, global.HostedServices.Get<BlockNotifier>(), folder, rpc, coordinatorToTest.RoundConfig);
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
		(_, IRPCClient rpc, Network network, Coordinator coordinator, _, _, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		Money denomination = Money.Coins(0.2m);
		decimal coordinatorFeePercent = 0.2m;
		int anonymitySet = 2;
		int connectionConfirmationTimeout = 50;
		var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 2, 0.7, coordinatorFeePercent, anonymitySet, 100, connectionConfirmationTimeout, 50, 50, 2, 24, false, 11);
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);

		IHttpClient httpClient = RegTestFixture.BackendHttpClient;
		var satoshiClient = new SatoshiClient(BackendClearnetHttpClient);

		#region PostInputsGetStates

		// <-------------------------->
		// POST INPUTS and GET STATES tests
		// <-------------------------->

		await Task.Delay(200).ConfigureAwait(false);
		var states = await satoshiClient.GetAllRoundStatesAsync();
		Assert.Single(states);
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
		var inputsRequest = new InputsRequest4
		{
			BlindedOutputScripts = null!, // null is not allowed and that's what we want here: try with invalid requests.
			ChangeOutputAddress = null!,
			Inputs = null!,
		};

		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out CoordinatorRound? round));
		var roundId = round!.RoundId;
		inputsRequest.RoundId = roundId;
		var registeredAddresses = Array.Empty<BitcoinAddress>();
		var signerPubKeys = round.MixingLevels.SignerPubKeys;
		var requesters = Array.Empty<Requester>();

		HttpRequestException httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Contains(HttpStatusCode.BadRequest.ToReasonString(), httpRequestException.Message);

		CompactSignature dummySignature = new(0, new byte[64]);

		BlindedOutputWithNonceIndex nullBlindedScript = new(0, uint256.One);
		inputsRequest.BlindedOutputScripts = Enumerable.Range(0, round.MixingLevels.Count() + 1).Select(x => nullBlindedScript);
		inputsRequest.ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
		inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(uint256.One, 0), Proof = dummySignature } };
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nToo many blinded output was provided", httpRequestException.Message);

		inputsRequest.BlindedOutputScripts = Enumerable.Range(0, round.MixingLevels.Count() - 2).Select(x => nullBlindedScript);
		inputsRequest.ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
		inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(uint256.One, 0), Proof = dummySignature } };
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nDuplicate blinded output found", httpRequestException.Message);

		inputsRequest.BlindedOutputScripts = new[] { nullBlindedScript };
		inputsRequest.ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
		inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(uint256.One, 0), Proof = dummySignature } };
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is not unspent", httpRequestException.Message);

		var addr = await rpc.GetNewAddressAsync();
		var hash = await rpc.SendToAddressAsync(addr, Money.Coins(0.01m));
		var tx = await rpc.GetRawTransactionAsync(hash);
		var coin = tx.Outputs.GetCoins(addr.ScriptPubKey).Single();

		inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = coin.Outpoint, Proof = dummySignature } };
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is unconfirmed.", httpRequestException.Message);

		var blocks = await rpc.GenerateAsync(1);
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input must be witness_v0_keyhash.", httpRequestException.Message);

		var blockHash = blocks.Single();
		var block = await rpc.GetBlockAsync(blockHash);
		var coinbase = block.Transactions.First();
		inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = new OutPoint(coinbase.GetHash(), 0), Proof = dummySignature } };
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided input is immature.", httpRequestException.Message);

		var key = new Key();
		var witnessAddress = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
		hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
		await rpc.GenerateAsync(1);
		tx = await rpc.GetRawTransactionAsync(hash);
		coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
		inputsRequest.Inputs = new List<InputProofModel> { new InputProofModel { Input = coin.Outpoint, Proof = dummySignature } };
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}", httpRequestException.Message);

		var proof = key.SignCompact(uint256.One);
		inputsRequest.Inputs.First().Proof = proof;
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nProvided proof is invalid.", httpRequestException.Message);

		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		var requester = new Requester();
		uint256 msg = new(NBitcoin.Crypto.Hashes.SHA256(network.Consensus.ConsensusFactory.CreateTransaction().ToBytes()));
		var nonce = round!.NonceProvider.GetNextNonce();
		BlindedOutputWithNonceIndex blindedData = new(nonce.N, requester.BlindMessage(msg, nonce.R, round.MixingLevels.GetBaseLevel().SignerKey.PubKey));
		inputsRequest.BlindedOutputScripts = new[] { blindedData };
		uint256 blindedOutputScriptsHash = new(NBitcoin.Crypto.Hashes.SHA256(blindedData.BlindedOutput.ToBytes()));

		proof = key.SignCompact(blindedOutputScriptsHash);
		inputsRequest.Inputs.First().Proof = proof;
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

		roundConfig.Denomination = Money.Coins(0.01m); // exactly the same as our output
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		roundId = round!.RoundId;
		inputsRequest.RoundId = roundId;
		signerPubKeys = round.MixingLevels.SignerPubKeys;
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

		roundConfig.Denomination = Money.Coins(0.00999999m); // one satoshi less than our output
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		roundId = round!.RoundId;
		inputsRequest.RoundId = roundId;
		signerPubKeys = round.MixingLevels.SignerPubKeys;
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nNot enough inputs are provided. Fee to pay:", httpRequestException.Message);

		roundConfig.Denomination = Money.Coins(0.008m); // one satoshi less than our output
		roundConfig.ConnectionConfirmationTimeout = 7;
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		roundId = round!.RoundId;
		inputsRequest.RoundId = roundId;
		signerPubKeys = round.MixingLevels.SignerPubKeys;
		requester = new Requester();
		requesters = new[] { requester };
		msg = network.Consensus.ConsensusFactory.CreateTransaction().GetHash();
		nonce = round.NonceProvider.GetNextNonce();
		blindedData = new BlindedOutputWithNonceIndex(nonce.N, requester.BlindMessage(msg, nonce.R, round.MixingLevels.GetBaseLevel().SignerKey.PubKey));
		inputsRequest.BlindedOutputScripts = new[] { blindedData };
		blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.BlindedOutput.ToBytes()));
		proof = key.SignCompact(blindedOutputScriptsHash);
		inputsRequest.Inputs.First().Proof = proof;
		var aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest);
		{
			// Test DelayedClientRoundRegistration logic.
			ClientRoundRegistration first;
			var randomKey = KeyManager.CreateNew(out _, "", network).GenerateNewKey(SmartLabel.Empty, KeyState.Clean, false);
			var second = new ClientRoundRegistration(aliceClient,
				new[] { BitcoinFactory.CreateSmartCoin(randomKey, 0m, anonymitySet: 2) },
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

			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
			Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nBlinded output has already been registered.", httpRequestException.Message);

			roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
			Assert.Equal(RoundPhase.InputRegistration, roundState.Phase);
			Assert.Equal(1, roundState.RegisteredPeerCount);

			roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
			Assert.Equal(RoundPhase.InputRegistration, roundState.Phase);
			Assert.Equal(1, roundState.RegisteredPeerCount);
			await aliceClient.PostUnConfirmationAsync();
		}

		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		requester = new Requester();
		nonce = round!.NonceProvider.GetNextNonce();
		blindedData = new BlindedOutputWithNonceIndex(nonce.N, requester.BlindScript(round.MixingLevels.GetBaseLevel().SignerKey.PubKey, nonce.R, key.PubKey.ScriptPubKey));
		inputsRequest.BlindedOutputScripts = new[] { blindedData };
		blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.BlindedOutput.ToBytes()));
		proof = key.SignCompact(blindedOutputScriptsHash);
		inputsRequest.Inputs.First().Proof = proof;

		Assert.True(coordinator.TryGetRound(roundId, out CoordinatorRound? currentRound));
		Assert.Equal(RoundPhase.InputRegistration, currentRound!.Phase);
		Assert.Equal(2, currentRound.AnonymitySet);
		Assert.Equal(0, currentRound.CountAlices());

		aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest);
		{
			Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
			Assert.True(aliceClient.RoundId > 0);

			Assert.Equal(2, currentRound.AnonymitySet);
			Assert.Equal(1, currentRound.CountAlices());

			var roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
			Assert.Equal(RoundPhase.InputRegistration, roundState.Phase);
			Assert.Equal(1, roundState.RegisteredPeerCount);
		}

		inputsRequest.BlindedOutputScripts = new[] { nullBlindedScript };
		blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(uint256.One.ToBytes()));
		proof = key.SignCompact(blindedOutputScriptsHash);
		inputsRequest.Inputs.First().Proof = proof;
		inputsRequest.Inputs = new List<InputProofModel> { inputsRequest.Inputs.First() };

		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nNonce 0 was already used.", httpRequestException.Message);

		nonce = round.NonceProvider.GetNextNonce();
		blindedData = new BlindedOutputWithNonceIndex(nonce.N, RandomUtils.GetUInt256());
		inputsRequest.BlindedOutputScripts = new[] { blindedData };
		blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(uint256.One.ToBytes()));
		proof = key.SignCompact(blindedOutputScriptsHash);
		inputsRequest.Inputs.First().Proof = proof;
		inputsRequest.Inputs = new List<InputProofModel> { inputsRequest.Inputs.First(), inputsRequest.Inputs.First() };
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nCannot register an input twice.", httpRequestException.Message);

		nonce = round.NonceProvider.GetNextNonce();
		blindedData = new BlindedOutputWithNonceIndex(nonce.N, requester.BlindScript(round.MixingLevels.GetBaseLevel().SignerKey.PubKey, nonce.R, key.PubKey.ScriptPubKey));
		inputsRequest.BlindedOutputScripts = new[] { blindedData };
		blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.BlindedOutput.ToBytes()));

		var inputProofs = new List<InputProofModel>();
		for (int j = 0; j < 8; j++)
		{
			key = new Key();
			witnessAddress = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
			hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
			await rpc.GenerateAsync(1);
			tx = await rpc.GetRawTransactionAsync(hash);
			coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
			proof = key.SignCompact(blindedOutputScriptsHash);
			inputProofs.Add(new InputProofModel { Input = coin.Outpoint, Proof = proof });
		}
		var blockHashed = await rpc.GenerateAsync(1);

		inputsRequest.Inputs = inputProofs;
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
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

		aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest);
		{
			Assert.Equal(2, currentRound.AnonymitySet);
			Assert.Equal(2, currentRound.CountAlices());
			Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
			Assert.True(aliceClient.RoundId > 0);

			await awaiter.WaitAsync(TimeSpan.FromSeconds(7));

			var roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
			Assert.Equal(RoundPhase.ConnectionConfirmation, roundState.Phase);
			Assert.Equal(2, roundState.RegisteredPeerCount);
			await Task.Delay(200).ConfigureAwait(false);
			var inputRegistrableRoundState = await satoshiClient.TryGetRegistrableRoundStateAsync();
			Assert.Equal(0, inputRegistrableRoundState?.RegisteredPeerCount);

			roundConfig.ConnectionConfirmationTimeout = 1; // One second.
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			await Task.Delay(200).ConfigureAwait(false);
			Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
			roundId = round!.RoundId;
			inputsRequest.RoundId = roundId;
			signerPubKeys = round.MixingLevels.SignerPubKeys;

			roundState = await satoshiClient.GetRoundStateAsync(aliceClient.RoundId);
			Assert.Equal(RoundPhase.ConnectionConfirmation, roundState.Phase);
			Assert.Equal(2, roundState.RegisteredPeerCount);
		}

		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.Equal($"{HttpStatusCode.BadRequest.ToReasonString()}\nInput is already registered in another round.", httpRequestException.Message);

		// Wait until input registration times out.
		await Task.Delay(TimeSpan.FromSeconds(8));
		httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputsRequest));
		Assert.StartsWith($"{HttpStatusCode.BadRequest.ToReasonString()}\nInput is banned from participation for", httpRequestException.Message);

		var spendingTx = network.Consensus.ConsensusFactory.CreateTransaction();
		var bannedCoin = inputsRequest.Inputs.First().Input;
		var utxos = coordinator.UtxoReferee;
		Assert.NotNull(await utxos.TryGetBannedAsync(bannedCoin, false));
		spendingTx.Inputs.Add(new TxIn(bannedCoin));
		spendingTx.Outputs.Add(new TxOut(Money.Coins(1), new Key().GetAddress(ScriptPubKeyType.Segwit, network)));
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
		witnessAddress = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
		hash = await rpc.SendToAddressAsync(witnessAddress, Money.Coins(0.01m));
		await rpc.GenerateAsync(1);
		tx = await rpc.GetRawTransactionAsync(hash);
		coin = tx.Outputs.GetCoins(witnessAddress.ScriptPubKey).Single();
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		requester = new Requester();
		requesters = new[] { requester };
		var bitcoinWitPubKeyAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
		registeredAddresses = new[] { bitcoinWitPubKeyAddress };
		Script script = bitcoinWitPubKeyAddress.ScriptPubKey;
		nonce = round!.NonceProvider.GetNextNonce();
		blindedData = new BlindedOutputWithNonceIndex(nonce.N, requester.BlindScript(round.MixingLevels.GetBaseLevel().SignerKey.PubKey, nonce.R, script));
		blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedData.BlindedOutput.ToBytes()));

		var inputRequest = new InputsRequest4
		{
			BlindedOutputScripts = new[] { blindedData },
			ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network),
			Inputs = new InputProofModel[]
			{
					new InputProofModel { Input = coin.Outpoint, Proof = key.SignCompact(blindedOutputScriptsHash) }
			}
		};
		aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputRequest);
		{
			Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
			Assert.True(aliceClient.RoundId > 0);
			// Double the request.
			// badrequests
			using (var response = await httpClient.SendAsync(HttpMethod.Post, $"/api/v{WalletWasabi.Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation"))
			{
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
			using (var response = await httpClient.SendAsync(HttpMethod.Post, $"/api/v{WalletWasabi.Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={aliceClient.UniqueId}"))
			{
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
			using (var response = await httpClient.SendAsync(HttpMethod.Post, $"/api/v{WalletWasabi.Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?roundId={aliceClient.RoundId}"))
			{
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			}
			using (var response = await httpClient.SendAsync(HttpMethod.Post, $"/api/v{WalletWasabi.Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId=foo&roundId={aliceClient.RoundId}"))
			{
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				Assert.Equal("Invalid uniqueId provided.", await response.Content.ReadAsJsonAsync<string>());
			}
			using (var response = await httpClient.SendAsync(HttpMethod.Post, $"/api/v{WalletWasabi.Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/confirmation?uniqueId={aliceClient.UniqueId}&roundId=bar"))
			{
				Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
				Assert.Contains("\"roundId\":[\"The value 'bar' is not valid.\"", await response.Content.ReadAsStringAsync());
			}

			roundConfig.ConnectionConfirmationTimeout = 60;
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			await Task.Delay(200).ConfigureAwait(false);
			Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
			roundId = round!.RoundId;
			inputsRequest.RoundId = roundId;
			signerPubKeys = round.MixingLevels.SignerPubKeys;
			httpRequestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await aliceClient.PostConfirmationAsync());
			Assert.Equal($"{HttpStatusCode.Gone.ToReasonString()}\nRound is not running.", httpRequestException.Message);
		}

		inputRequest = new InputsRequest4
		{
			BlindedOutputScripts = new[] { blindedData },
			ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network),
			Inputs = new InputProofModel[]
			{
					new InputProofModel { Input = coin.Outpoint, Proof = key.SignCompact(blindedOutputScriptsHash) }
			}
		};

		aliceClient = await CreateNewAliceClientAsync(roundId, registeredAddresses, signerPubKeys, requesters, inputRequest);
		{
			Assert.NotEqual(Guid.Empty, aliceClient.UniqueId);
			Assert.True(aliceClient.RoundId > 0);
			await aliceClient.PostUnConfirmationAsync();
			using HttpResponseMessage response = await httpClient.SendAsync(HttpMethod.Post, $"/api/v{WalletWasabi.Helpers.Constants.BackendMajorVersion}/btc/chaumiancoinjoin/unconfirmation?uniqueId={aliceClient.UniqueId}&roundId={aliceClient.RoundId}");
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
		var outputAddress1 = key1.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
		var outputAddress2 = key2.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
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

		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		signerPubKeys = round!.MixingLevels.SignerPubKeys;
		roundId = round.RoundId;

		var requester1 = new Requester();
		var requester2 = new Requester();
		nonce = round.NonceProvider.GetNextNonce();
		BlindedOutputWithNonceIndex blinded1 = new(nonce.N, requester1.BlindScript(round.MixingLevels.GetBaseLevel().SignerKey.PubKey, nonce.R, outputAddress1.ScriptPubKey));
		uint256 blindedOutputScriptsHash1 = new(NBitcoin.Crypto.Hashes.SHA256(blinded1.BlindedOutput.ToBytes()));
		nonce = round.NonceProvider.GetNextNonce();
		BlindedOutputWithNonceIndex blinded2 = new(nonce.N, requester2.BlindScript(round.MixingLevels.GetBaseLevel().Signer.Key.PubKey, nonce.R, outputAddress2.ScriptPubKey));
		uint256 blindedOutputScriptsHash2 = new(NBitcoin.Crypto.Hashes.SHA256(blinded2.BlindedOutput.ToBytes()));

		OutPoint input1 = new(hash1, index1);
		OutPoint input2 = new(hash2, index2);

		var inputRequest1 = new InputsRequest4
		{
			BlindedOutputScripts = new[] { blinded1 },
			ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network),
			Inputs = new InputProofModel[]
			{
					new InputProofModel { Input = input1, Proof = key1.SignCompact(blindedOutputScriptsHash1) }
			}
		};
		var inputRequest2 = new InputsRequest4
		{
			BlindedOutputScripts = new[] { blinded2 },
			ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network),
			Inputs = new InputProofModel[]
			{
					new InputProofModel { Input = input2, Proof = key2.SignCompact(blindedOutputScriptsHash2) }
			}
		};

		var aliceClient1 = await CreateNewAliceClientAsync(roundId, new[] { outputAddress1 }, signerPubKeys, new[] { requester1 }, inputRequest1);
		var aliceClient2 = await CreateNewAliceClientAsync(roundId, new[] { outputAddress2 }, signerPubKeys, new[] { requester2 }, inputRequest2);
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

			{
				var bobClient1 = new BobClient(BackendClearnetHttpClient);
				var bobClient2 = new BobClient(BackendClearnetHttpClient);
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
			Assert.Equal(2, unsignedCoinJoin.Outputs.Count); // Because the two inputs are equal, so change addresses won't be used, nor coordinator fee will be taken.
			Assert.Contains(input1, unsignedCoinJoin.Inputs.Select(x => x.PrevOut));
			Assert.Contains(input2, unsignedCoinJoin.Inputs.Select(x => x.PrevOut));
			Assert.Equal(2, unsignedCoinJoin.Inputs.Count);

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

			if (rpc is CachedRpcClient cachedRpc)
			{
				cachedRpc.Cache.Remove("GetRawMempoolAsync");
			}

			uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
			Assert.Contains(unsignedCoinJoin.GetHash(), mempooltxs);

			var wasabiClient = new WasabiClient(BackendClearnetHttpClient);
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
		(_, IRPCClient rpc, Network network, Coordinator coordinator, _, _, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		Money denomination = Money.Coins(0.1m);
		decimal coordinatorFeePercent = 0.0002m;
		int anonymitySet = 4;
		int connectionConfirmationTimeout = 50;
		var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 2, 0.7, coordinatorFeePercent, anonymitySet, 100, connectionConfirmationTimeout, 50, 50, 2, 24, false, 11);
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);

		var satoshiClient = new SatoshiClient(BackendClearnetHttpClient);
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out CoordinatorRound? round));
		var roundId = round!.RoundId;

		// We have to 4 participant so, this data structure is for keeping track of
		// important data for each of the participants in the coinjoin session.
		var participants = new List<(AliceClient4,
									 List<(Requester requester, BitcoinAddress outputAddress, BlindedOutputWithNonceIndex blindedScript)>,
									 List<(OutPoint input, CompactSignature proof, Coin coin, Key key)>)>();

		// INPUTS REGISTRATION PHASE --
		for (var anosetIdx = 0; anosetIdx < anonymitySet; anosetIdx++)
		{
			// Create as many outputs as mixin levels (even when we do not have funds enough)
			var outputs = new List<(Requester requester, BitcoinAddress outputAddress, BlindedOutputWithNonceIndex blindedScript)>();
			foreach (var level in round.MixingLevels.Levels)
			{
				var requester = new Requester();
				var outputsAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
				var scriptPubKey = outputsAddress.ScriptPubKey;
				// We blind the scriptPubKey with a new requester by mixin level
				var nonce = round.NonceProvider.GetNextNonce();
				BlindedOutputWithNonceIndex blindedScript = new(nonce.N, requester.BlindScript(level.SignerKey.PubKey, nonce.R, scriptPubKey));
				outputs.Add((requester, outputsAddress, blindedScript));
			}

			// Calculate the SHA256( blind1 || blind2 || .....|| blindN )
			var blindedOutputScriptList = outputs.Select(x => x.blindedScript);
			var blindedOutputScriptListBytes = ByteHelpers.Combine(blindedOutputScriptList.Select(x => x.BlindedOutput.ToBytes()));
			var blindedOutputScriptsHash = new uint256(NBitcoin.Crypto.Hashes.SHA256(blindedOutputScriptListBytes));

			// Create 4 new coins that we want to mix
			var inputs = new List<(OutPoint input, CompactSignature proof, Coin coin, Key key)>();
			for (var inputIdx = 0; inputIdx < 4; inputIdx++)
			{
				var key = new Key();
				var outputAddress = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				var hash = await rpc.SendToAddressAsync(outputAddress, Money.Coins(0.1m));
				await rpc.GenerateAsync(1);
				var tx = await rpc.GetRawTransactionAsync(hash);
				var index = tx.Outputs.FindIndex(x => x.ScriptPubKey == outputAddress.ScriptPubKey);
				OutPoint input = new(hash, index);

				inputs.Add((
					input,
					key.SignCompact(blindedOutputScriptsHash),
					new Coin(tx, (uint)index),
					key
				));
			}

			// Save alice client and the outputs, requesters, etc
			var inputRequest = new InputsRequest4
			{
				BlindedOutputScripts = blindedOutputScriptList,
				ChangeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network),
				Inputs = inputs.Select(x => new InputProofModel { Input = x.input, Proof = x.proof }).ToArray()
			};
			var aliceClient = await CreateNewAliceClientAsync(
				round.RoundId,
				outputs.Select(x => x.outputAddress),
				round.MixingLevels.SignerPubKeys,
				outputs.Select(x => x.requester),
				inputRequest);

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
		var bobClient = new BobClient(BackendClearnetHttpClient);

		foreach (var (aliceClient, outputs, _) in participants)
		{
			var i = 0;
			foreach (var output in outputs.Take(activeOutputs[l].Count()))
			{
				await bobClient.PostOutputAsync(aliceClient.RoundId, new ActiveOutput(output.outputAddress, activeOutputs[l].ElementAt(i).Signature, i));
				i++;
			}
			l++;
		}

		// SIGNING PHASE --
		roundState = await satoshiClient.GetRoundStateAsync(roundId);
		Assert.Equal(RoundPhase.Signing, roundState.Phase);

		uint256? transactionId = null;
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

		if (rpc is CachedRpcClient cachedRpc)
		{
			cachedRpc.Cache.Remove("GetRawMempoolAsync");
		}

		uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
		Assert.Contains(transactionId, mempooltxs);
	}

	[Fact]
	public async Task Ccj100ParticipantsTestsAsync()
	{
		(_, IRPCClient rpc, Network network, Coordinator coordinator, _, _, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		Money denomination = Money.Coins(0.1m);
		decimal coordinatorFeePercent = 0.003m;
		int anonymitySet = 100;
		int connectionConfirmationTimeout = 120;
		var roundConfig = RegTestFixture.CreateRoundConfig(denomination, WalletWasabi.Helpers.Constants.OneDayConfirmationTarget, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);
		await rpc.GenerateAsync(100); // So to make sure we have enough money.

		Uri baseUri = new(RegTestFixture.BackendEndPoint);
		var spentCoins = new List<Coin>();
		var fundingTxCount = 0;
		var inputRegistrationUsers = new List<(Requester requester, BlindedOutputWithNonceIndex blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData)>();
		for (int i = 0; i < roundConfig.AnonymitySet; i++)
		{
			var userInputData = new List<(Key key, BitcoinAddress inputAddress, uint256 txHash, Transaction tx, OutPoint input)>();
			var activeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
			var changeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);

			Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out CoordinatorRound? round));
			var requester = new Requester();
			var nonce = round!.NonceProvider.GetNextNonce();
			BlindedOutputWithNonceIndex blinded = new(nonce.N, requester.BlindScript(round.MixingLevels.GetBaseLevel().SignerKey.PubKey, nonce.R, activeOutputAddress.ScriptPubKey));
			uint256 blindedOutputScriptsHash = new(NBitcoin.Crypto.Hashes.SHA256(blinded.BlindedOutput.ToBytes()));

			var inputProofModels = new List<InputProofModel>();
			int numberOfInputs = CryptoHelpers.RandomInt(1, 6);
			var receiveSatoshiSum = 0;
			for (int j = 0; j < numberOfInputs; j++)
			{
				var key = new Key();
				var receiveSatoshi = CryptoHelpers.RandomInt(1000, 100000000);
				receiveSatoshiSum += receiveSatoshi;
				if (j == numberOfInputs - 1)
				{
					receiveSatoshi = 100000000;
				}
				var inputAddress = key.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
				uint256 txHash = await rpc.SendToAddressAsync(inputAddress, Money.Satoshis(receiveSatoshi));
				fundingTxCount++;
				Assert.NotNull(txHash);
				Transaction transaction = await rpc.GetRawTransactionAsync(txHash);

				var coin = transaction.Outputs.GetCoins(inputAddress.ScriptPubKey).Single();
				spentCoins.Add(coin);

				OutPoint input = coin.Outpoint;
				var inputProof = new InputProofModel { Input = input, Proof = key.SignCompact(blindedOutputScriptsHash) };
				inputProofModels.Add(inputProof);

				GetTxOutResponse getTxOutResponse = (await rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true))!;
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

		var aliceClients = new List<Task<AliceClient4>>();

		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out CoordinatorRound? currentRound));

		foreach (var user in inputRegistrationUsers)
		{
			aliceClients.Add(CreateNewAliceClientAsync(currentRound!.RoundId,
				new[] { user.activeOutputAddress },
				currentRound.MixingLevels.SignerPubKeys,
				new[] { user.requester },
				new InputsRequest4
				{
					ChangeOutputAddress = user.changeOutputAddress,
					BlindedOutputScripts = new[] { user.blinded },
					Inputs = user.inputProofModels
				}));
		}

		long roundId = 0;
		var users = new List<(Requester requester, BlindedOutputWithNonceIndex blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient4 aliceClient)>();
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
			users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient));
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
		var usersWithSignatures = new List<(Requester requester, BlindedOutputWithNonceIndex blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient4 aliceClient, UnblindedSignature unblindedSignature)>();
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
			usersWithSignatures.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, user.aliceClient, resp.Item2.First().Signature));
		}

		var outputRequests = new List<Task>();
		foreach (var user in usersWithSignatures)
		{
			var bobClient = new BobClient(BackendClearnetHttpClient);
			outputRequests.Add(bobClient.PostOutputAsync(roundId, new ActiveOutput(user.activeOutputAddress, user.unblindedSignature, 0)));
		}

		foreach (Task task in outputRequests)
		{
			await task;
		}

		var coinjoinRequests = new List<Task<Transaction>>();
		foreach (var user in usersWithSignatures)
		{
			coinjoinRequests.Add(user.aliceClient.GetUnsignedCoinJoinAsync());
		}

		var unsignedCoinJoin = await coinjoinRequests.First();
		var unsignedCoinJoinHex = unsignedCoinJoin.ToHex();
		Assert.All(coinjoinRequests, async x => Assert.Equal(unsignedCoinJoinHex, (await x).ToHex()));

		var signatureRequests = new List<Task>();
		foreach (var user in usersWithSignatures)
		{
			var partSignedCj = Transaction.Parse(unsignedCoinJoinHex, network);
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

		if (rpc is CachedRpcClient cachedRpc)
		{
			cachedRpc.Cache.Remove("GetRawMempoolAsync");
		}
		uint256[] mempooltxs = await rpc.GetRawMempoolAsync();
		Assert.Contains(unsignedCoinJoin.GetHash(), mempooltxs);

		var coins = new List<Coin>();
		var finalCoinjoin = await rpc.GetRawTransactionAsync(mempooltxs.First());
		foreach (var input in finalCoinjoin.Inputs)
		{
			GetTxOutResponse getTxOut = (await rpc.GetTxOutAsync(input.PrevOut.Hash, (int)input.PrevOut.N, includeMempool: false))!;
			Assert.NotNull(getTxOut);

			coins.Add(new Coin(input.PrevOut.Hash, input.PrevOut.N, getTxOut.TxOut.Value, getTxOut.TxOut.ScriptPubKey));
		}

		FeeRate feeRateTx = finalCoinjoin.GetFeeRate(coins.ToArray());
		FeeRate feeRateReal = (await rpc.EstimateSmartFeeAsync(roundConfig.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true)).FeeRate;

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
		(string password, IRPCClient rpc, Network network, Coordinator coordinator, _, BitcoinStore bitcoinStore, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.

		Money denomination = Money.Coins(0.9m);
		decimal coordinatorFeePercent = 0.1m;
		int anonymitySet = 7;
		int connectionConfirmationTimeout = 14;
		var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);

		var participants = new List<(SmartCoin, CoinJoinClient)>();

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

			var keyManager = KeyManager.CreateNew(out _, password, network);
			var key = keyManager.GenerateNewKey("foo", KeyState.Clean, false);
			var bech = key.GetP2wpkhAddress(network);
			var txId = await rpc.SendToAddressAsync(bech, amount, replaceable: false);
			key.SetKeyState(KeyState.Used);
			var tx = await rpc.GetRawTransactionAsync(txId);
			var height = await rpc.GetBlockCountAsync();
			SmartTransaction stx = new(tx, height + 1);
			var bechCoin = tx.Outputs.GetCoins(bech.ScriptPubKey).Single();

			SmartCoin smartCoin = new(stx, bechCoin.Outpoint.N, key);
			key.SetAnonymitySet(tx.GetAnonymitySet(bechCoin.Outpoint.N), tx.GetHash());

			CoinJoinClient chaumianClient = new(synchronizer, rpc.Network, keyManager, new Kitchen());

			participants.Add((smartCoin, chaumianClient));
		}

		await rpc.GenerateAsync(1);

		try
		{
			// 2. Start mixing.
			foreach (var participant in participants)
			{
				SmartCoin coin = participant.Item1;

				var chaumianClient = participant.Item2;
				chaumianClient.Start();

				await chaumianClient.QueueCoinsToMixAsync(password, coin);
			}

			Task timeout = Task.Delay(TimeSpan.FromSeconds(connectionConfirmationTimeout * 2 + 7 * 2 + 7 * 2 + 7 * 2));
			while ((await rpc.GetRawMempoolAsync()).Length == 0)
			{
				if (timeout.IsCompletedSuccessfully)
				{
					throw new TimeoutException("Coinjoin was not propagated.");
				}

				await Task.Delay(1000);
			}
		}
		finally
		{
			foreach (var participant in participants)
			{
				SmartCoin coin = participant.Item1;
				var chaumianClient = participant.Item2;

				Task timeout = Task.Delay(3000);
				while (chaumianClient.State.GetActivelyMixingRounds().Any())
				{
					if (timeout.IsCompletedSuccessfully)
					{
						throw new TimeoutException("Coinjoin was not noticed.");
					}
					await Task.Delay(1000);
				}

				if (chaumianClient is { })
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
		(string password, IRPCClient rpc, Network network, Coordinator coordinator, _, BitcoinStore bitcoinStore, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
		synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.

		Money denomination = Money.Coins(0.1m);
		decimal coordinatorFeePercent = 0.1m;
		int anonymitySet = 2;
		int connectionConfirmationTimeout = 14;
		var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");
		await Task.Delay(200).ConfigureAwait(false);
		await rpc.GenerateAsync(3); // So to make sure we have enough money.
		var keyManager = KeyManager.CreateNew(out _, password, network);
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
		SmartTransaction stx1 = new(tx1, height);
		SmartTransaction stx2 = new(tx2, height);
		SmartTransaction stx3 = new(tx3, height);
		SmartTransaction stx4 = new(tx4, height);

		var bech1Coin = tx1.Outputs.GetCoins(bech1.ScriptPubKey).Single();
		var bech2Coin = tx2.Outputs.GetCoins(bech2.ScriptPubKey).Single();
		var bech3Coin = tx3.Outputs.GetCoins(bech3.ScriptPubKey).Single();
		var bech4Coin = tx4.Outputs.GetCoins(bech4.ScriptPubKey).Single();

		SmartCoin smartCoin1 = new(stx1, bech1Coin.Outpoint.N, key1);
		SmartCoin smartCoin2 = new(stx2, bech2Coin.Outpoint.N, key2);
		SmartCoin smartCoin3 = new(stx3, bech3Coin.Outpoint.N, key3);
		SmartCoin smartCoin4 = new(stx4, bech4Coin.Outpoint.N, key4);
		key1.SetAnonymitySet(tx1.GetAnonymitySet(bech1Coin.Outpoint.N), tx1.GetHash());
		key2.SetAnonymitySet(tx2.GetAnonymitySet(bech2Coin.Outpoint.N), tx2.GetHash());
		key3.SetAnonymitySet(tx3.GetAnonymitySet(bech3Coin.Outpoint.N), tx3.GetHash());
		key4.SetAnonymitySet(tx4.GetAnonymitySet(bech4Coin.Outpoint.N), tx4.GetHash());

		CoinJoinClient chaumianClient1 = new(synchronizer, rpc.Network, keyManager, new Kitchen());
		CoinJoinClient chaumianClient2 = new(synchronizer, rpc.Network, keyManager, new Kitchen());
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

			var randomKey = keyManager.GenerateNewKey(SmartLabel.Empty, KeyState.Clean, isInternal: false);
			// Make sure it does not throw.
			var randomTx = network.Consensus.ConsensusFactory.CreateTransaction();
			randomTx.Outputs.Add(new TxOut(Money.Coins(3m), randomKey.P2wpkhScript));
			SmartTransaction randomStx = new(randomTx, Height.Mempool);
			await chaumianClient1.DequeueCoinsFromMixAsync(new SmartCoin(randomStx, 0, randomKey), DequeueReason.UserRequested);
			randomKey.SetAnonymitySet(1, randomStx.GetHash());

			Assert.Equal(2, (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
			await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin1, DequeueReason.UserRequested);
			Assert.False(smartCoin1.CoinJoinInProgress);
			await chaumianClient1.DequeueCoinsFromMixAsync(new[] { smartCoin1, smartCoin2 }, DequeueReason.UserRequested);
			Assert.False(smartCoin1.CoinJoinInProgress);
			Assert.False(smartCoin2.CoinJoinInProgress);
			Assert.Equal(2, (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
			Assert.True(smartCoin1.CoinJoinInProgress);
			Assert.True(smartCoin2.CoinJoinInProgress);
			await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin1, DequeueReason.UserRequested);
			await chaumianClient1.DequeueCoinsFromMixAsync(smartCoin2, DequeueReason.UserRequested);
			Assert.False(smartCoin1.CoinJoinInProgress);
			Assert.False(smartCoin2.CoinJoinInProgress);

			Assert.Equal(2, (await chaumianClient1.QueueCoinsToMixAsync(password, smartCoin1, smartCoin2)).Count());
			Assert.True(smartCoin1.CoinJoinInProgress);
			Assert.True(smartCoin2.CoinJoinInProgress);
			Assert.Single((await chaumianClient2.QueueCoinsToMixAsync(password, smartCoin3)));

			Task timeout = Task.Delay(TimeSpan.FromSeconds(connectionConfirmationTimeout * 2 + 7 * 2 + 7 * 2 + 7 * 2));
			while ((await rpc.GetRawMempoolAsync()).Length == 0)
			{
				if (timeout.IsCompletedSuccessfully)
				{
					throw new TimeoutException("Coinjoin was not propagated.");
				}
				await Task.Delay(1000);
			}

			var cjHash = (await rpc.GetRawMempoolAsync()).Single();
			var cj = await rpc.GetRawTransactionAsync(cjHash);
			SmartTransaction sCj = new(cj, Height.Mempool);
			smartCoin1.SpenderTransaction = sCj;
			smartCoin2.SpenderTransaction = sCj;
			smartCoin3.SpenderTransaction = sCj;

			// Make sure if times out, it tries again.
			connectionConfirmationTimeout = 1;
			roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 50, 50, 1, 24, true, 11);
			coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
			coordinator.AbortAllRoundsInInputRegistration("");
			await Task.Delay(200).ConfigureAwait(false);
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
			if (chaumianClient1 is { })
			{
				await chaumianClient1.StopAsync(CancellationToken.None);
			}
			if (chaumianClient2 is { })
			{
				await chaumianClient2.StopAsync(CancellationToken.None);
			}
		}
	}

	private async Task<AliceClient4> CreateNewAliceClientAsync(
		long roundId,
		IEnumerable<BitcoinAddress> registeredAddresses,
		IEnumerable<PubKey> signerPubKeys,
		IEnumerable<Requester> requesters,
		InputsRequest4 request)
	{
		return await AliceClientBase.CreateNewAsync(
			roundId,
			registeredAddresses,
			signerPubKeys,
			requesters,
			Network.RegTest,
			request.ChangeOutputAddress,
			request.BlindedOutputScripts,
			request.Inputs,
			BackendClearnetHttpClient);
	}
}

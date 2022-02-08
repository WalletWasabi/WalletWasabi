using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Tor.Http;
using Xunit;
using static WalletWasabi.Crypto.SchnorrBlinding;
using UnblindedSignature = WalletWasabi.Crypto.UnblindedSignature;

namespace WalletWasabi.Tests.RegressionTests;

[Collection("RegTest collection")]
public class DosTests
{
	public DosTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;

		BackendHttpClient = regTestFixture.BackendHttpClient;
		SatoshiClient = new SatoshiClient(BackendHttpClient);
	}

	private RegTestFixture RegTestFixture { get; }
	public SatoshiClient SatoshiClient { get; }
	public IHttpClient BackendHttpClient { get; }

	private async Task WaitForTimeoutAsync()
	{
		var times = 0;
		while (!(await SatoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == RoundPhase.InputRegistration))
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
		(_, IRPCClient rpc, Network network, Coordinator coordinator, _, _, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		Money denomination = Money.Coins(0.1m);
		decimal coordinatorFeePercent = 0.1m;
		int anonymitySet = 3;
		int connectionConfirmationTimeout = 120;
		var roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 1, 1, 1, 24, true, 11);
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");

		await rpc.GenerateAsync(3); // So to make sure we have enough money.

		Uri baseUri = new(RegTestFixture.BackendEndPoint);
		var fundingTxCount = 0;
		List<(Requester requester, BlindedOutputWithNonceIndex blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData)> inputRegistrationUsers = new();
		CoordinatorRound? round = null;
		for (int i = 0; i < roundConfig.AnonymitySet; i++)
		{
			List<(Key key, BitcoinAddress inputAddress, uint256 txHash, Transaction tx, OutPoint input)> userInputData = new();
			var activeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
			var changeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
			Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
			Requester requester = new();
			var nonce = round!.NonceProvider.GetNextNonce();

			BlindedOutputWithNonceIndex blinded = new(nonce.N, requester.BlindScript(round.MixingLevels.GetBaseLevel().SignerKey.PubKey, nonce.R, activeOutputAddress.ScriptPubKey));
			uint256 blindedOutputScriptsHash = new(Hashes.SHA256(blinded.BlindedOutput.ToBytes()));

			List<InputProofModel> inputProofModels = new();
			int numberOfInputs = CryptoHelpers.RandomInt(1, 6);
			var receiveSatoshiSum = 0;
			for (int j = 0; j < numberOfInputs; j++)
			{
				Key key = new();
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

				OutPoint input = coin.Outpoint;
				var inputProof = new InputProofModel { Input = input, Proof = key.SignCompact(blindedOutputScriptsHash) };
				inputProofModels.Add(inputProof);

				GetTxOutResponse? getTxOutResponse = await rpc.GetTxOutAsync(input.Hash, (int)input.N, includeMempool: true);

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

		List<Task<AliceClient4>> aliceClients = new();

		foreach (var user in inputRegistrationUsers)
		{
			aliceClients.Add(AliceClientBase.CreateNewAsync(round!.RoundId, new[] { user.activeOutputAddress }, new[] { round!.MixingLevels.GetBaseLevel().SignerKey.PubKey }, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, BackendHttpClient));
		}

		long roundId = 0;
		List<(Requester requester, BlindedOutputWithNonceIndex blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient4 aliceClient, UnblindedSignature unblindedSignature)> users = new();
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

			// Because it's a value tuple.
			users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient, null!));
		}

		Assert.Equal(users.Count, roundConfig.AnonymitySet);

		List<Task<(RoundPhase currentPhase, IEnumerable<ActiveOutput>)>> confirmationRequests = new();

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

		{
			var times = 0;
			while (!(await SatoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == RoundPhase.InputRegistration))
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
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));
		foreach (var user in inputRegistrationUsers)
		{
			aliceClients.Add(AliceClientBase.CreateNewAsync(round!.RoundId, new[] { user.activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SignerKey.PubKey }, new[] { user.requester }, network, user.changeOutputAddress, new[] { user.blinded }, user.inputProofModels, BackendHttpClient));
		}

		roundId = 0;
		users = new List<(Requester requester, BlindedOutputWithNonceIndex blinded, BitcoinAddress activeOutputAddress, BitcoinAddress changeOutputAddress, IEnumerable<InputProofModel> inputProofModels, List<(Key key, BitcoinAddress address, uint256 txHash, Transaction tx, OutPoint input)> userInputData, AliceClient4 aliceClient, UnblindedSignature unblindedSignature)>();
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

			// Because it's a value tuple.
			users.Add((user.requester, user.blinded, user.activeOutputAddress, user.changeOutputAddress, user.inputProofModels, user.userInputData, aliceClient, null!));
		}

		Assert.Equal(users.Count, roundConfig.AnonymitySet);

		confirmationRequests = new List<Task<(RoundPhase currentPhase, IEnumerable<ActiveOutput>)>>();

		foreach (var user in users)
		{
			confirmationRequests.Add(user.aliceClient.PostConfirmationAsync());
		}

		{
			var times = 0;
			while (!(await SatoshiClient.GetAllRoundStatesAsync()).All(x => x.Phase == RoundPhase.InputRegistration))
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
		(_, IRPCClient rpc, Network network, Coordinator coordinator, _, _, _) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		Money denomination = Money.Coins(1m);
		decimal coordinatorFeePercent = 0.1m;
		int anonymitySet = 2;
		int connectionConfirmationTimeout = 1;
		bool doesNoteBeforeBan = true;
		CoordinatorRoundConfig roundConfig = RegTestFixture.CreateRoundConfig(denomination, 140, 0.7, coordinatorFeePercent, anonymitySet, 240, connectionConfirmationTimeout, 1, 1, 1, 24, doesNoteBeforeBan, 11);
		coordinator.RoundConfig.UpdateOrDefault(roundConfig, toFile: true);
		coordinator.AbortAllRoundsInInputRegistration("");

		Uri baseUri = new(RegTestFixture.BackendEndPoint);

		List<(BitcoinAddress changeOutputAddress, BlindedOutputWithNonceIndex blindedData, InputProofModel[] inputsProofs)> registerRequests = new();
		AliceClient4? aliceClientBackup = null;
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out CoordinatorRound? round));
		for (int i = 0; i < roundConfig.AnonymitySet; i++)
		{
			var activeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
			var changeOutputAddress = new Key().GetAddress(ScriptPubKeyType.Segwit, network);
			Key inputKey = new();
			var inputAddress = inputKey.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);

			Requester requester = new();
			var nonce = round!.NonceProvider.GetNextNonce();

			BlindedOutputWithNonceIndex blinded = new(nonce.N, requester.BlindScript(round.MixingLevels.GetBaseLevel().SignerKey.PubKey, nonce.R, activeOutputAddress.ScriptPubKey));
			uint256 blindedOutputScriptsHash = new(Hashes.SHA256(blinded.BlindedOutput.ToBytes()));

			uint256 txHash = await rpc.SendToAddressAsync(inputAddress, Money.Coins(2));
			await rpc.GenerateAsync(1);
			Transaction transaction = await rpc.GetRawTransactionAsync(txHash);
			Coin coin = transaction.Outputs.GetCoins(inputAddress.ScriptPubKey).Single();
			OutPoint input = coin.Outpoint;

			InputProofModel inputProof = new() { Input = input, Proof = inputKey.SignCompact(blindedOutputScriptsHash) };
			InputProofModel[] inputsProofs = new InputProofModel[] { inputProof };
			registerRequests.Add((changeOutputAddress, blinded, inputsProofs));
			aliceClientBackup = await AliceClientBase.CreateNewAsync(round.RoundId, new[] { activeOutputAddress }, new[] { round.MixingLevels.GetBaseLevel().SignerKey.PubKey }, new[] { requester }, network, changeOutputAddress, new[] { blinded }, inputsProofs, BackendHttpClient);
		}

		await WaitForTimeoutAsync();

		int bannedCount = coordinator.UtxoReferee.CountBanned(false);
		Assert.Equal(0, bannedCount);
		int notedCount = coordinator.UtxoReferee.CountBanned(true);
		Assert.Equal(anonymitySet, notedCount);
		Assert.True(coordinator.TryGetCurrentInputRegisterableRound(out round));

		foreach (var registerRequest in registerRequests)
		{
			await AliceClientBase.CreateNewAsync(round!.RoundId, aliceClientBackup!.RegisteredAddresses, round.MixingLevels.GetAllLevels().Select(x => x.SignerKey.PubKey), aliceClientBackup.Requesters, network, registerRequest.changeOutputAddress, new[] { registerRequest.blindedData }, registerRequest.inputsProofs, BackendHttpClient);
		}

		await WaitForTimeoutAsync();

		bannedCount = coordinator.UtxoReferee.CountBanned(false);
		Assert.Equal(anonymitySet, bannedCount);
		notedCount = coordinator.UtxoReferee.CountBanned(true);
		Assert.Equal(anonymitySet, notedCount);
	}
}

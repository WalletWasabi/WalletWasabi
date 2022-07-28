using Microsoft.Extensions.Caching.Memory;
using Moq;
using NBitcoin;
using NBitcoin.RPC;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Controllers;
using WalletWasabi.Backend.Controllers.WabiSabi;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class ArenaClientTests
{
	[Fact]
	public async Task FullP2wpkhCoinjoinTestAsync()
	{
		await TestFullCoinjoinAsync(ScriptPubKeyType.Segwit, Constants.P2wpkhInputVirtualSize);
	}

	[Fact]
	public async Task RemoveInputAsyncTestAsync()
	{
		var config = new WabiSabiConfig();
		var round = WabiSabiFactory.CreateRound(config);
		round.SetPhase(Phase.ConnectionConfirmation);
		var fundingTx = BitcoinFactory.CreateSmartTransaction(ownOutputCount: 1);
		var coin = fundingTx.WalletOutputs.First().Coin;
		var alice = new Alice(coin, new OwnershipProof(), round, Guid.NewGuid(), false);
		round.Alices.Add(alice);

		using Arena arena = await ArenaBuilder.From(config).CreateAndStartAsync(round);

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var idempotencyRequestCache = new IdempotencyRequestCache(memoryCache);
		using CoinJoinFeeRateStatStore coinJoinFeeRateStatStore = new(config, arena.Rpc);
		var wabiSabiApi = new WabiSabiController(idempotencyRequestCache, arena, coinJoinFeeRateStatStore);

		var apiClient = new ArenaClient(null!, null!, config.CoordinatorIdentifier, wabiSabiApi);

		round.SetPhase(Phase.InputRegistration);

		await apiClient.RemoveInputAsync(round.Id, alice.Id, CancellationToken.None);
		Assert.Empty(round.Alices);
	}

	private async Task TestFullCoinjoinAsync(ScriptPubKeyType scriptPubKeyType, int inputVirtualSize)
	{
		var config = new WabiSabiConfig { MaxInputCountByRound = 1 };
		var round = WabiSabiFactory.CreateRound(WabiSabiFactory.CreateRoundParameters(config));
		using var key = new Key();
		var outpoint = BitcoinFactory.CreateOutPoint();
		var mockRpc = new Mock<IRPCClient>();
		mockRpc.Setup(rpc => rpc.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, true, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
			{
				IsCoinBase = false,
				Confirmations = 200,
				TxOut = new TxOut(Money.Coins(1m), key.PubKey.GetAddress(scriptPubKeyType, Network.Main)),
			});
		mockRpc.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), It.IsAny<EstimateSmartFeeMode>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new EstimateSmartFeeResponse
			{
				Blocks = 1000,
				FeeRate = new FeeRate(10m)
			});
		mockRpc.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new MemPoolInfo
			{
				MinRelayTxFee = 1
			});
		mockRpc.Setup(rpc => rpc.PrepareBatch()).Returns(mockRpc.Object);
		mockRpc.Setup(rpc => rpc.SendBatchAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
		mockRpc.Setup(rpc => rpc.GetRawTransactionAsync(It.IsAny<uint256>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(BitcoinFactory.CreateTransaction());

		using Arena arena = await ArenaBuilder.From(config).With(mockRpc).CreateAndStartAsync(round);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

		using var memoryCache = new MemoryCache(new MemoryCacheOptions());
		var idempotencyRequestCache = new IdempotencyRequestCache(memoryCache);

		using CoinJoinFeeRateStatStore coinJoinFeeRateStatStore = new(config, arena.Rpc);
		var wabiSabiApi = new WabiSabiController(idempotencyRequestCache, arena, coinJoinFeeRateStatStore);

		var insecureRandom = new InsecureRandom();
		var roundState = RoundState.FromRound(round);
		var aliceArenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(insecureRandom),
			roundState.CreateVsizeCredentialClient(insecureRandom),
			config.CoordinatorIdentifier,
			wabiSabiApi);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id, scriptPubKeyType);

		var (inputRegistrationResponse, _) = await aliceArenaClient.RegisterInputAsync(round.Id, outpoint, ownershipProof, CancellationToken.None);
		var aliceId = inputRegistrationResponse.Value;

		var amountsToRequest = new[]
		{
			Money.Coins(.75m) - round.Parameters.MiningFeeRate.GetFee(inputVirtualSize) - round.Parameters.CoordinationFeeRate.GetFee(Money.Coins(1m)),
			Money.Coins(.25m),
		}.Select(x => x.Satoshi).ToArray();

		using var destinationKey1 = new Key();
		using var destinationKey2 = new Key();
		var scriptSize = (long)destinationKey1.PubKey.GetScriptPubKey(scriptPubKeyType).EstimateOutputVsize();

		var vsizesToRequest = new[] { round.Parameters.MaxVsizeAllocationPerAlice - (inputVirtualSize + 2 * scriptSize), 2 * scriptSize };

		// Phase: Input Registration
		Assert.Equal(Phase.InputRegistration, round.Phase);

		var connectionConfirmationResponse1 = await aliceArenaClient.ConfirmConnectionAsync(
			round.Id,
			aliceId,
			amountsToRequest,
			vsizesToRequest,
			inputRegistrationResponse.IssuedAmountCredentials,
			inputRegistrationResponse.IssuedVsizeCredentials,
			CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
		Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

		// Phase: Connection Confirmation
		var connectionConfirmationResponse2 = await aliceArenaClient.ConfirmConnectionAsync(
			round.Id,
			aliceId,
			amountsToRequest,
			vsizesToRequest,
			connectionConfirmationResponse1.IssuedAmountCredentials,
			connectionConfirmationResponse1.IssuedVsizeCredentials,
			CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

		// Phase: Output Registration
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		var bobArenaClient = new ArenaClient(
			roundState.CreateAmountCredentialClient(insecureRandom),
			roundState.CreateVsizeCredentialClient(insecureRandom),
			config.CoordinatorIdentifier,
			wabiSabiApi);

		var reissuanceResponse = await bobArenaClient.ReissueCredentialAsync(
			round.Id,
			amountsToRequest,
			Enumerable.Repeat(scriptSize, 2),
			connectionConfirmationResponse2.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber),
			connectionConfirmationResponse2.IssuedVsizeCredentials.Skip(1).Take(ProtocolConstants.CredentialNumber), // first amount is the leftover value
			CancellationToken.None);

		Credential amountCred1 = reissuanceResponse.IssuedAmountCredentials.ElementAt(0);
		Credential amountCred2 = reissuanceResponse.IssuedAmountCredentials.ElementAt(1);
		Credential zeroAmountCred1 = reissuanceResponse.IssuedAmountCredentials.ElementAt(2);
		Credential zeroAmountCred2 = reissuanceResponse.IssuedAmountCredentials.ElementAt(3);

		Credential vsizeCred1 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(0);
		Credential vsizeCred2 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(1);
		Credential zeroVsizeCred1 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(2);
		Credential zeroVsizeCred2 = reissuanceResponse.IssuedVsizeCredentials.ElementAt(3);

		await bobArenaClient.RegisterOutputAsync(
			round.Id,
			destinationKey1.PubKey.GetScriptPubKey(scriptPubKeyType),
			new[] { amountCred1, zeroAmountCred1 },
			new[] { vsizeCred1, zeroVsizeCred1 },
			CancellationToken.None);

		await bobArenaClient.RegisterOutputAsync(
			round.Id,
			destinationKey2.PubKey.GetScriptPubKey(scriptPubKeyType),
			new[] { amountCred2, zeroAmountCred2 },
			new[] { vsizeCred2, zeroVsizeCred2 },
			CancellationToken.None);

		await aliceArenaClient.ReadyToSignAsync(round.Id, aliceId, CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Single(tx.Inputs);
		Assert.Equal(2 + 1, tx.Outputs.Count); // +1 because it pays coordination fees
	}
}

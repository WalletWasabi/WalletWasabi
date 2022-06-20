using Moq;
using NBitcoin;
using NBitcoin.RPC;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;
using Xunit.Abstractions;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping;

public class StepTransactionSigningTests
{
	[Fact]
	public async Task EveryoneSignedAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5,
			MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await aliceClient1.ReadyToSignAsync(CancellationToken.None);
		await aliceClient2.ReadyToSignAsync(CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateTransaction();

		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.DoesNotContain(round, arena.GetActiveRounds());
		Assert.Equal(Phase.Ended, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task FailsBroadcastAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5,
			MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.Setup(rpc => rpc.SendRawTransactionAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null));

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await aliceClient1.ReadyToSignAsync(CancellationToken.None);
		await aliceClient2.ReadyToSignAsync(CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateTransaction();

		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.Equal(EndRoundState.TransactionBroadcastFailed, round.EndRoundState);
		Assert.Empty(prison.GetInmates());

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AlicesSpentAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5,
			MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
		};

		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.Setup(rpc => rpc.SendRawTransactionAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null));

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();

		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await aliceClient1.ReadyToSignAsync(CancellationToken.None);
		await aliceClient2.ReadyToSignAsync(CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateTransaction();

		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.Equal(EndRoundState.TransactionBroadcastFailed, round.EndRoundState);

		// There should be no inmate, because we aren't punishing spenders with banning
		// as there's no reason to ban already spent UTXOs,
		// the cost of spending the UTXO is the punishment instead.
		Assert.Empty(prison.GetInmates());

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TimeoutInsufficientPeersAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 1,
			TransactionSigningTimeout = TimeSpan.Zero,
			MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.Setup(rpc => rpc.SendRawTransactionAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null));

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await aliceClient1.ReadyToSignAsync(CancellationToken.None);
		await aliceClient2.ReadyToSignAsync(CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateTransaction();
		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.Equal(EndRoundState.AbortedNotEnoughAlicesSigned, round.EndRoundState);
		Assert.Empty(arena.Rounds.Where(x => x is BlameRound));
		Assert.Contains(aliceClient2.SmartCoin.OutPoint, prison.GetInmates().Select(x => x.Utxo));

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TimeoutSufficientPeersAsync()
	{
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 1,
			TransactionSigningTimeout = TimeSpan.Zero,
			OutputRegistrationTimeout = TimeSpan.Zero,
			FailFastTransactionSigningTimeout = TimeSpan.Zero,
			FailFastOutputRegistrationTimeout = TimeSpan.Zero,
			MaxSuggestedAmountBase = Money.Satoshis(ProtocolConstants.MaxAmountPerAlice)
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.Setup(rpc => rpc.SendRawTransactionAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null));

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		// Make sure not all alices signed.
		var alice3 = WabiSabiFactory.CreateAlice(round);
		alice3.ConfirmedConnection = true;
		round.Alices.Add(alice3);
		round.CoinjoinState = round.Assert<ConstructionState>().AddInput(alice3.Coin);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateTransaction();
		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, CancellationToken.None);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Single(arena.Rounds.Where(x => x is BlameRound));
		var badOutpoint = alice3.Coin.Outpoint;
		Assert.Contains(badOutpoint, prison.GetInmates().Select(x => x.Utxo));

		var onlyRound = arena.Rounds.Single(x => x is BlameRound);
		var blameRound = Assert.IsType<BlameRound>(onlyRound);
		Assert.NotNull(blameRound.BlameOf);
		Assert.Equal(round.Id, blameRound.BlameOf.Id);

		var whitelist = blameRound.BlameWhitelist;
		Assert.Contains(aliceClient1.SmartCoin.OutPoint, whitelist);
		Assert.Contains(aliceClient2.SmartCoin.OutPoint, whitelist);
		Assert.DoesNotContain(badOutpoint, whitelist);

		await arena.StopAsync(CancellationToken.None);
	}

	private async Task<(Round Round, AliceClient AliceClient1, AliceClient AliceClient2)>
			CreateRoundWithOutputsReadyToSignAsync(Arena arena, IKeyChain keyChain, SmartCoin coin1, SmartCoin coin2)
	{
		// Create the round.
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		var round = Assert.Single(arena.Rounds);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		// Register Alices.
		using RoundStateUpdater roundStateUpdater = new(TimeSpan.FromSeconds(2), arena);
		await roundStateUpdater.StartAsync(CancellationToken.None);

		var task1 = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, coin1, keyChain, roundStateUpdater, CancellationToken.None);
		var task2 = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, coin2, keyChain, roundStateUpdater, CancellationToken.None);

		while (Phase.OutputRegistration != round.Phase)
		{
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		}

		await Task.WhenAll(task1, task2);

		var aliceClient1 = task1.Result;
		var aliceClient2 = task2.Result;

		// Register outputs.
		var bobClient = new BobClient(round.Id, arenaClient);
		using var destKey1 = new Key();
		using var destKey2 = new Key();
		await bobClient.RegisterOutputAsync(
			destKey1.PubKey.WitHash.ScriptPubKey,
			aliceClient1.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber),
			aliceClient1.IssuedVsizeCredentials.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None).ConfigureAwait(false);

		await bobClient.RegisterOutputAsync(
			destKey1.PubKey.WitHash.ScriptPubKey,
			aliceClient2.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber),
			aliceClient2.IssuedVsizeCredentials.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None).ConfigureAwait(false);

		await roundStateUpdater.StopAsync(CancellationToken.None);
		return (round, aliceClient1, aliceClient2);
	}
}

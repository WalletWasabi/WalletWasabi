using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Coordinator.WabiSabi;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds.Utils;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping;

public class StepTransactionSigningTests
{
	private TimeSpan TestTimeout { get; } = TimeSpan.FromMinutes(3);

	[Fact]
	public async Task EveryoneSignedAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5,
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransactionWithPrecomputedData();

		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await arena.TriggerAndWaitRoundAsync(token);
		Assert.DoesNotContain(round, arena.GetActiveRounds());
		Assert.Equal(Phase.Ended, round.Phase);

		await arena.StopAsync(token);
	}

	[Fact]
	public async Task FailsBroadcastAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		WabiSabiConfig cfg = WabiSabiFactory.CreateWabiSabiConfig();
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.OnSendRawTransactionAsync = (_) =>
			throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransactionWithPrecomputedData();

		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await arena.TriggerAndWaitRoundAsync(token);
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.Equal(EndRoundState.TransactionBroadcastFailed, round.EndRoundState);

		var now = DateTimeOffset.UtcNow;
		Assert.All(
			new[] { aliceClient1.SmartCoin.Outpoint, aliceClient2.SmartCoin.Outpoint },
			prevOut => Assert.False(prison.IsBanned(prevOut, cfg.GetDoSConfiguration(), now)));

		await arena.StopAsync(token);
	}

	[Fact]
	public async Task AlicesSpentAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		WabiSabiConfig cfg = WabiSabiFactory.CreateWabiSabiConfig();
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.OnSendRawTransactionAsync = (_) =>
			throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();

		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransactionWithPrecomputedData();

		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await arena.TriggerAndWaitRoundAsync(token);
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.Equal(EndRoundState.TransactionBroadcastFailed, round.EndRoundState);

		// There should be no inmate, because we aren't punishing spenders with banning
		// as there's no reason to ban already spent UTXOs,
		// the cost of spending the UTXO is the punishment instead.
		var now = DateTimeOffset.UtcNow;
		Assert.All(
			new[] { aliceClient1.SmartCoin.Outpoint, aliceClient2.SmartCoin.Outpoint },
			prevOut => Assert.False(prison.IsBanned(prevOut, cfg.GetDoSConfiguration(), now)));

		await arena.StopAsync(token);
	}

	[Fact]
	public async Task TimeoutInsufficientPeersAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		var cfg = WabiSabiFactory.CreateWabiSabiConfig() with
		{
			MinInputCountByRoundMultiplier = 1,
			MinInputCountByBlameRoundMultiplier = 1,
			TransactionSigningTimeout = TimeSpan.Zero,
		};

		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.OnSendRawTransactionAsync = (_) =>
			throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransactionWithPrecomputedData();
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await arena.TriggerAndWaitRoundAsync(token);
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.Equal(EndRoundState.AbortedNotEnoughAlicesSigned, round.EndRoundState);
		Assert.DoesNotContain(arena.Rounds, x => x is BlameRound);

		Assert.True(prison.IsBanned(aliceClient1.SmartCoin.Outpoint, cfg.GetDoSConfiguration(), DateTimeOffset.UtcNow));

		await arena.StopAsync(token);
	}

	[Fact]
	public async Task TimeoutSufficientPeersAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		var cfg = WabiSabiFactory.CreateWabiSabiConfig() with
		{
			MinInputCountByRoundMultiplier = 1,
			TransactionSigningTimeout = TimeSpan.Zero,
			FailFastOutputRegistrationTimeout = TimeSpan.Zero,
		};

		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		mockRpc.OnSendRawTransactionAsync = (_) =>
			throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync();
		var (round, aliceClient1, aliceClient2) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coin1, coin2);

		// Make sure not all alices signed.
		var alice3 = WabiSabiFactory.CreateAlice(round);
		alice3.ConfirmedConnection = true;
		alice3.ReadyToSign = true;
		round.Alices.Add(alice3);
		round.CoinjoinState = round.Assert<ConstructionState>().AddInput(alice3.Coin, alice3.OwnershipProof, WabiSabiFactory.CreateCommitmentData(round.Id));
		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var signedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransactionWithPrecomputedData();
		await aliceClient1.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await aliceClient2.SignTransactionAsync(signedCoinJoin, keyChain, token);
		await arena.TriggerAndWaitRoundAsync(token);
		Assert.DoesNotContain(round, arena.Rounds.Where(x => x.Phase != Phase.Ended));
		Assert.Single(arena.Rounds, x => x is BlameRound);
		var badOutpoint = alice3.Coin.Outpoint;
		Assert.True(prison.IsBanned(badOutpoint, cfg.GetDoSConfiguration(), DateTimeOffset.UtcNow));

		var onlyRound = arena.Rounds.Single(x => x is BlameRound);
		var blameRound = Assert.IsType<BlameRound>(onlyRound);
		Assert.NotNull(blameRound.BlameOf);
		Assert.Equal(round.Id, blameRound.BlameOf.Id);

		var whitelist = blameRound.BlameWhitelist;
		Assert.Contains(aliceClient1.SmartCoin.Outpoint, whitelist);
		Assert.Contains(aliceClient2.SmartCoin.Outpoint, whitelist);
		Assert.DoesNotContain(badOutpoint, whitelist);

		await arena.StopAsync(token);
	}

	/// <summary>
	/// Verifies how the coordinator treats Alices that did not send a ready-to-sign request and Alices that did not sign.
	/// </summary>
	/// <remarks>
	/// Not sending a ready-to-sign request is fine if the Alice signs. Alices who do not sign are banned
	/// and excluded from the blame round, whether or not they signaled ready.
	/// </remarks>
	[Theory(Timeout = 180_000)]
	[InlineData(1, false, false, true, true, true, 0)]     // Good and bad Alices are not ready, but everybody signs.
	[InlineData(2, true, false, false, true, true, 1)]     // Bad Alice is not ready and does not sign. Others are ready and sign.
	[InlineData(3, false, false, false, true, true, 1)]    // Good Alice is not ready but signs. Bad Alice is not ready and does not sign.
	[InlineData(4, false, true, false, true, true, 1)]     // Bad Alice is ready but does not sign.
	[InlineData(5, false, false, false, true, false, 2)]   // Bad Alice is not ready and does not sign. Third Alice is ready but does not sign either.
	[InlineData(6, false, false, true, true, false, 1)]    // Only the third Alice does not sign, and she is ready. Non-ready Alices signed, so they are forgiven.
	[InlineData(7, false, false, false, false, false, 2)]  // Only the good Alice signs. Bad and third Alices are not ready and do not sign.
	public async Task AliceWasNotReadyAsync(int testId, bool goodReady, bool badReady, bool badSigns, bool thirdReady, bool thirdSigns, int expectedBans)
	{
		Assert.True(testId > 0);
		var token = TestContext.Current.CancellationToken;

		var cfg = WabiSabiFactory.CreateWabiSabiConfig() with
		{
			MaxInputCountByRound = 3,
			DoSMinTimeInPrison = TimeSpan.FromMinutes(1),
			TransactionSigningTimeout = TimeSpan.Zero,
			OutputRegistrationTimeout = TimeSpan.Zero,
			FailFastTransactionSigningTimeout = TimeSpan.Zero,
		};

		var (keyChain, coins) = WabiSabiFactory.CreateCoinKeyPairs(3);

		var mockRpcClient = WabiSabiFactory.CreatePreconfiguredRpcClient(coins[0].Coin, coins[1].Coin, coins[2].Coin);
		mockRpcClient.OnSendRawTransactionAsync = (tx) =>
			throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

		var prison = WabiSabiFactory.CreatePrison();

		using var arena = await ArenaBuilder.From(cfg, mockRpcClient, prison).CreateAndStartAsync();
		var (round, aliceClients) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, coins[0], coins[1], coins[2]);
		var (badAliceClient, goodAliceClient, thirdAliceClient) = (aliceClients[0], aliceClients[1], aliceClients[2]);

		var badOutpoint = badAliceClient.SmartCoin.Coin.Outpoint;
		var goodOutpoint = goodAliceClient.SmartCoin.Coin.Outpoint;
		var thirdOutpoint = thirdAliceClient.SmartCoin.Coin.Outpoint;

		Assert.Equal(3, round.Alices.Count);

		// Configure the ready-to-sign state for each Alice.
		round.Alices.Single(x => goodOutpoint == x.Coin.Outpoint).ReadyToSign = goodReady;
		round.Alices.Single(x => badOutpoint == x.Coin.Outpoint).ReadyToSign = badReady;
		round.Alices.Single(x => thirdOutpoint == x.Coin.Outpoint).ReadyToSign = thirdReady;

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);

		var unsignedCoinJoin = round.Assert<SigningState>().CreateUnsignedTransactionWithPrecomputedData();

		// Good Alice always signs.
		await goodAliceClient.SignTransactionAsync(unsignedCoinJoin, keyChain, token);

		if (badSigns)
		{
			await badAliceClient.SignTransactionAsync(unsignedCoinJoin, keyChain, token);
		}

		if (thirdSigns)
		{
			await thirdAliceClient.SignTransactionAsync(unsignedCoinJoin, keyChain, token);
		}

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.Ended, round.Phase);
		Assert.True(round.FastSigningPhase);

		var dos = cfg.GetDoSConfiguration();
		var now = DateTimeOffset.UtcNow;

		var signers = new List<OutPoint> { goodOutpoint };
		var nonSigners = new List<OutPoint>();
		(badSigns ? signers : nonSigners).Add(badOutpoint);
		(thirdSigns ? signers : nonSigners).Add(thirdOutpoint);

		// Alices who signed are never banned, even when they did not send ready-to-sign.
		Assert.All(signers, outpoint => Assert.False(prison.IsBanned(outpoint, dos, now)));

		if (nonSigners.Count == 0)
		{
			Assert.Equal(3, signers.Count);

			Assert.Equal(EndRoundState.TransactionBroadcastFailed, round.EndRoundState);

			// Nobody is punished and there is no blame round.
			Assert.DoesNotContain(arena.Rounds, x => x is BlameRound);
			Assert.Equal(0, expectedBans);
			Assert.All(signers, outpoint => Assert.False(prison.IsBanned(outpoint, dos, now), $"{outpoint} is banned!"));
		}
		else
		{
			Assert.Equal(EndRoundState.NotAllAlicesSign, round.EndRoundState);

			// Everybody who did not sign is banned, whether or not she signaled its readiness.
			Assert.All(nonSigners, outpoint => Assert.True(prison.IsBanned(outpoint, dos, now), $"{outpoint} is not banned!"));
			Assert.Equal(expectedBans, nonSigners.Count);

			var blameRound = Assert.IsType<BlameRound>(Assert.Single(arena.Rounds, x => x is BlameRound));
			Assert.Equal(round.Id, blameRound.BlameOf.Id);

			// Only Alices who signed are whitelisted.
			Assert.Equal(signers.OrderBy(x => x.ToString()), blameRound.BlameWhitelist.OrderBy(x => x.ToString()));
		}

		await arena.StopAsync(token);
	}

	private async Task<(Round Round, AliceClient AliceClient1, AliceClient AliceClient2)> CreateRoundWithOutputsReadyToSignAsync(Arena arena, IKeyChain keyChain, SmartCoin coin1, SmartCoin coin2)
	{
		var (round, aliceClients) = await CreateRoundWithOutputsReadyToSignAsync(arena, keyChain, [coin1, coin2]).ConfigureAwait(false);
		return (round, aliceClients[0], aliceClients[1]);
	}

	private async Task<(Round Round, AliceClient[] AliceClients)> CreateRoundWithOutputsReadyToSignAsync(Arena arena, IKeyChain keyChain, params SmartCoin[] coins)
	{
		using var cancellationTokenSource = new CancellationTokenSource(TestTimeout);
		var token = cancellationTokenSource.Token;

		// Create the round.
		await arena.TriggerAndWaitRoundAsync(token).ConfigureAwait(false);

		var round = Assert.Single(arena.Rounds);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		// Register Alices.
		using var roundStateUpdater = RoundStateUpdaterForTesting.Create(arena);
		var roundStateProvider = new RoundStateProvider(roundStateUpdater);

		var aliceTasks = coins
			.Select(coin => AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, coin, keyChain, roundStateProvider, token, token, token))
			.ToArray();

		while (Phase.OutputRegistration != round.Phase)
		{
			await arena.TriggerAndWaitRoundAsync(token).ConfigureAwait(false);
		}

		var aliceClients = await Task.WhenAll(aliceTasks);

		// Register outputs.
		var bobClient = new BobClient(round.Id, arenaClient);
		foreach (var aliceClient in aliceClients)
		{
			using var destKey = new Key();
			await bobClient.RegisterOutputAsync(
				destKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit),
				aliceClient.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber),
				aliceClient.IssuedVsizeCredentials.Take(ProtocolConstants.CredentialNumber),
				token).ConfigureAwait(false);
		}

		foreach (var aliceClient in aliceClients)
		{
			await aliceClient.ReadyToSignAsync(token).ConfigureAwait(false);
		}

		return (round, aliceClients);
	}
}

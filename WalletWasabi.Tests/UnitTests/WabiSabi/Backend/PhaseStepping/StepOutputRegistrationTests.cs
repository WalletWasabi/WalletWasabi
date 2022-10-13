using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping;

public class StepOutputRegistrationTests
{
	private TimeSpan TestTimeout { get; } = TimeSpan.FromMinutes(3);

	[Fact]
	public async Task AllBobsRegisteredAsync()
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
		var (round, arenaClient, alices) = await CreateRoundWithTwoConfirmedConnectionsAsync(arena, keyChain, coin1, coin2);
		var (amountCredentials1, vsizeCredentials1) = (alices[0].IssuedAmountCredentials, alices[0].IssuedVsizeCredentials);
		var (amountCredentials2, vsizeCredentials2) = (alices[1].IssuedAmountCredentials, alices[1].IssuedVsizeCredentials);

		// Register outputs.
		var bobClient = new BobClient(round.Id, arenaClient);
		using var destKey1 = new Key();
		await bobClient.RegisterOutputAsync(
			destKey1.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit),
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			token);

		using var destKey2 = new Key();
		await bobClient.RegisterOutputAsync(
			destKey2.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit),
			amountCredentials2.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials2.Take(ProtocolConstants.CredentialNumber),
			token);

		foreach (var alice in alices)
		{
			await alice.ReadyToSignAsync(token);
		}

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);
		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Equal(2, tx.Inputs.Count);
		Assert.Equal(2 + 1, tx.Outputs.Count); // +1 for the coordinator fee

		await arena.StopAsync(token);
	}

	[Fact]
	public async Task SomeBobsRegisteredTimeoutAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5,
			OutputRegistrationTimeout = TimeSpan.Zero,
			CoordinationFeeRate = CoordinationFeeRate.Zero
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync();
		var (round, arenaClient, alices) = await CreateRoundWithTwoConfirmedConnectionsAsync(arena, keyChain, coin1, coin2);
		var (amountCredentials1, vsizeCredentials1) = (alices[0].IssuedAmountCredentials, alices[0].IssuedVsizeCredentials);
		var (amountCredentials2, vsizeCredentials2) = (alices[1].IssuedAmountCredentials, alices[1].IssuedVsizeCredentials);

		// Register outputs.
		var bobClient = new BobClient(round.Id, arenaClient);
		using var destKey = new Key();
		await bobClient.RegisterOutputAsync(
			destKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit),
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			token);

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);
		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Equal(2, tx.Inputs.Count);
		Assert.Equal(2, tx.Outputs.Count);
		Assert.Contains(round.CoordinatorScript, tx.Outputs.Select(x => x.ScriptPubKey));

		await arena.StopAsync(token);
	}

	[Fact]
	public async Task DiffTooSmallToBlameAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5,
			OutputRegistrationTimeout = TimeSpan.Zero,
			CoordinationFeeRate = CoordinationFeeRate.Zero
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync();
		var (round, arenaClient, alices) = await CreateRoundWithTwoConfirmedConnectionsAsync(arena, keyChain, coin1, coin2);
		var (amountCredentials1, vsizeCredentials1) = (alices[0].IssuedAmountCredentials, alices[0].IssuedVsizeCredentials);
		var (amountCredentials2, vsizeCredentials2) = (alices[1].IssuedAmountCredentials, alices[1].IssuedVsizeCredentials);

		// Register outputs.
		var bobClient = new BobClient(round.Id, arenaClient);
		using var destKey1 = new Key();
		using var destKey2 = new Key();
		await bobClient.RegisterOutputAsync(
			destKey1.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit),
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			token);

		await bobClient.RegisterOutputAsync(
			destKey2.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit),
			amountCredentials2.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials2.Take(ProtocolConstants.CredentialNumber),
			token);

		// Add another input. The input must be able to pay for itself, but
		// the remaining amount after deducting the fees needs to be less
		// than the minimum.
		var txParams = round.Parameters;
		var extraAlice = WabiSabiFactory.CreateAlice(round.Parameters.MiningFeeRate.GetFee(Constants.P2wpkhInputVirtualSize) + txParams.AllowedOutputAmounts.Min - new Money(1L), round);
		round.Alices.Add(extraAlice);
		round.CoinjoinState = round.Assert<ConstructionState>().AddInput(extraAlice.Coin, extraAlice.OwnershipProof, WabiSabiFactory.CreateCommitmentData(round.Id));

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.TransactionSigning, round.Phase);
		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Equal(3, tx.Inputs.Count);
		Assert.Equal(2, tx.Outputs.Count);
		Assert.DoesNotContain(round.CoordinatorScript, tx.Outputs.Select(x => x.ScriptPubKey));

		await arena.StopAsync(token);
	}

	[Fact]
	public async Task DoesntSwitchImmaturelyAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;
		
		WabiSabiConfig cfg = new()
		{
			MaxInputCountByRound = 2,
			MinInputCountByRoundMultiplier = 0.5
		};
		var (keyChain, coin1, coin2) = WabiSabiFactory.CreateCoinKeyPairs();

		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin1.Coin, coin2.Coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync();
		var (round, arenaClient, alices) = await CreateRoundWithTwoConfirmedConnectionsAsync(arena, keyChain, coin1, coin2);
		var (amountCredentials1, vsizeCredentials1) = (alices[0].IssuedAmountCredentials, alices[0].IssuedVsizeCredentials);
		var (amountCredentials2, vsizeCredentials2) = (alices[1].IssuedAmountCredentials, alices[1].IssuedVsizeCredentials);

		// Register outputs.
		var bobClient = new BobClient(round.Id, arenaClient);
		using var destKey = new Key();
		await bobClient.RegisterOutputAsync(
			destKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit),
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			token);

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		await arena.StopAsync(token);
	}

	private async Task<(Round Round, ArenaClient ArenaClient, AliceClient[] alices)>
			CreateRoundWithTwoConfirmedConnectionsAsync(Arena arena, IKeyChain keyChain, SmartCoin coin1, SmartCoin coin2)
	{
		using CancellationTokenSource cancellationTokenSource = new(TestTimeout);
		var token = cancellationTokenSource.Token;

		// Get the round.
		await arena.TriggerAndWaitRoundAsync(token);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var round = Assert.Single(arena.Rounds);

		// Refresh the Arena States because of vsize manipulation.
		await arena.TriggerAndWaitRoundAsync(token);

		using RoundStateUpdater roundStateUpdater = new(TimeSpan.FromSeconds(2), arena);
		await roundStateUpdater.StartAsync(token);
		var task1 = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, coin1, keyChain, roundStateUpdater, token, token, token);
		var task2 = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, coin2, keyChain, roundStateUpdater, token, token, token);

		while (Phase.ConnectionConfirmation != round.Phase)
		{
			await arena.TriggerAndWaitRoundAsync(token);
		}
		await Task.WhenAll(task1, task2);
		var aliceClient1 = await task1;
		var aliceClient2 = await task2;

		await arena.TriggerAndWaitRoundAsync(token);
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		await roundStateUpdater.StopAsync(token);

		return (round,
				arenaClient,
				new[]
				{
						aliceClient1,
						aliceClient2
				});
	}
}

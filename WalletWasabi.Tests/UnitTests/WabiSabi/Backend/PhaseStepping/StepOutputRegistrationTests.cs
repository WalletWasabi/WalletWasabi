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
	[Fact]
	public async Task AllBobsRegisteredAsync()
	{
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
			destKey1.PubKey.WitHash.ScriptPubKey,
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None);

		using var destKey2 = new Key();
		await bobClient.RegisterOutputAsync(
			destKey2.PubKey.WitHash.ScriptPubKey,
			amountCredentials2.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials2.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None);

		foreach (var alice in alices)
		{
			await alice.ReadyToSignAsync(CancellationToken.None);
		}

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);
		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Equal(2, tx.Inputs.Count);
		Assert.Equal(2 + 1, tx.Outputs.Count); // +1 for the coordinator fee

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SomeBobsRegisteredTimeoutAsync()
	{
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
			destKey.PubKey.WitHash.ScriptPubKey,
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);
		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Equal(2, tx.Inputs.Count);
		Assert.Equal(2, tx.Outputs.Count);
		Assert.Contains(round.CoordinatorScript, tx.Outputs.Select(x => x.ScriptPubKey));

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DiffTooSmallToBlameAsync()
	{
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
			destKey1.PubKey.WitHash.ScriptPubKey,
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None);

		await bobClient.RegisterOutputAsync(
			destKey2.PubKey.WitHash.ScriptPubKey,
			amountCredentials2.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials2.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None);

		// Add another input. The input must be able to pay for itself, but
		// the remaining amount after deducting the fees needs to be less
		// than the minimum.
		var txParams = round.Parameters;
		var extraAlice = WabiSabiFactory.CreateAlice(round.Parameters.MiningFeeRate.GetFee(Constants.P2wpkhInputVirtualSize) + txParams.AllowedOutputAmounts.Min - new Money(1L), round);
		round.Alices.Add(extraAlice);
		round.CoinjoinState = round.Assert<ConstructionState>().AddInput(extraAlice.Coin);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.TransactionSigning, round.Phase);
		var tx = round.Assert<SigningState>().CreateTransaction();
		Assert.Equal(3, tx.Inputs.Count);
		Assert.Equal(2, tx.Outputs.Count);
		Assert.DoesNotContain(round.CoordinatorScript, tx.Outputs.Select(x => x.ScriptPubKey));

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task DoesntSwitchImmaturelyAsync()
	{
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
			destKey.PubKey.WitHash.ScriptPubKey,
			amountCredentials1.Take(ProtocolConstants.CredentialNumber),
			vsizeCredentials1.Take(ProtocolConstants.CredentialNumber),
			CancellationToken.None);

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		await arena.StopAsync(CancellationToken.None);
	}

	private async Task<(Round Round, ArenaClient ArenaClient, AliceClient[] alices)>
			CreateRoundWithTwoConfirmedConnectionsAsync(Arena arena, IKeyChain keyChain, SmartCoin coin1, SmartCoin coin2)
	{
		// Get the round.
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var round = Assert.Single(arena.Rounds);

		// Refresh the Arena States because of vsize manipulation.
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		using RoundStateUpdater roundStateUpdater = new(TimeSpan.FromSeconds(2), arena);
		await roundStateUpdater.StartAsync(CancellationToken.None);
		var task1 = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, coin1, keyChain, roundStateUpdater, CancellationToken.None);
		var task2 = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, coin2, keyChain, roundStateUpdater, CancellationToken.None);

		while (Phase.ConnectionConfirmation != round.Phase)
		{
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		}
		await Task.WhenAll(task1, task2);
		var aliceClient1 = await task1;
		var aliceClient2 = await task2;

		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(Phase.OutputRegistration, round.Phase);

		await roundStateUpdater.StopAsync(CancellationToken.None);

		return (round,
				arenaClient,
				new[]
				{
						aliceClient1,
						aliceClient2
				});
	}
}

using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class AliceTimeoutTests
{
	[Fact]
	public async Task AliceTimesoutAsync()
	{
		// Alice times out when its deadline is reached.
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var km = ServiceFactory.CreateKeyManager("");
		var key = BitcoinFactory.CreateHdPubKey(km);
		var smartCoin = BitcoinFactory.CreateSmartCoin(key, 10m);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(smartCoin.Coin);

		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);

		using RoundStateUpdater roundStateUpdater = new(TimeSpan.FromSeconds(2), arena);
		await roundStateUpdater.StartAsync(CancellationToken.None);

		// Register Alices.
		var keyChain = new KeyChain(km, new Kitchen(""));

		using CancellationTokenSource cancellationTokenSource = new();
		var task = AliceClient.CreateRegisterAndConfirmInputAsync(RoundState.FromRound(round), arenaClient, smartCoin, keyChain, roundStateUpdater, cancellationTokenSource.Token);

		while (round.Alices.Count == 0)
		{
			await Task.Delay(10);
		}

		var alice = Assert.Single(round.Alices);
		alice.Deadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		Assert.Empty(round.Alices);

		cancellationTokenSource.Cancel();

		try
		{
			await task;
			throw new InvalidOperationException("The operation should throw!");
		}
		catch (Exception exc)
		{
			Assert.True(exc is OperationCanceledException or WabiSabiProtocolException);
		}

		await roundStateUpdater.StopAsync(CancellationToken.None);
		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceDoesntTimeoutInConnectionConfirmationAsync()
	{
		// Alice does not time out when it's not input registration anymore,
		// even though the deadline is reached.
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.ConnectionConfirmation);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);

		Assert.Single(round.Alices);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Single(round.Alices);
		Assert.Equal(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceDoesntTimeoutIfInputRegistrationTimedoutAsync()
	{
		// Alice does not time out if input registration timed out,
		// even though the deadline is reached and still in input reg.
		WabiSabiConfig cfg = new() { StandardInputRegistrationTimeout = TimeSpan.Zero };
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);

		Assert.Single(round.Alices);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Single(round.Alices);
		Assert.Equal(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceDoesntTimeoutIfMaxInputCountReachedAsync()
	{
		// Alice does not time out if input reg is full with alices,
		// even though the deadline is reached and still in input reg.
		WabiSabiConfig cfg = new() { MaxInputCountByRound = 3 };
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);

		Assert.Equal(3, round.Alices.Count);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Equal(3, round.Alices.Count);
		Assert.Equal(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceDeadlineUpdatedAsync()
	{
		// Alice's deadline is updated by connection confirmation.
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);

		Assert.Single(round.Alices);
		DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
		alice.Deadline = preDeadline;
		await arena.ConfirmConnectionAsync(req, CancellationToken.None);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
		Assert.Single(round.Alices);
		Assert.NotEqual(preDeadline, alice.Deadline);

		await arena.StopAsync(CancellationToken.None);
	}
}

using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Client;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class RegisterInputSuccessTests
{
	private static void AssertSingleAliceSuccessfullyRegistered(Round round, DateTimeOffset minAliceDeadline, ArenaResponse<Guid> resp)
	{
		var alice = Assert.Single(round.Alices);
		Assert.NotNull(resp);
		Assert.NotNull(resp.IssuedAmountCredentials);
		Assert.NotNull(resp.IssuedVsizeCredentials);
		Assert.True(minAliceDeadline <= alice.Deadline);
	}

	[Fact]
	public async Task SuccessAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var (resp, _) = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None);
		AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SuccessFromPreviousCoinJoinAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		var coinJoinIdStore = new CoinJoinIdStore();
		coinJoinIdStore.TryAdd(coin.Outpoint.Hash);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).With(coinJoinIdStore).CreateAndStartAsync(round);

		var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);

		var (resp, _) = await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None);
		AssertSingleAliceSuccessfullyRegistered(round, minAliceDeadline, resp);

		var myAlice = Assert.Single(round.Alices);
		Assert.True(myAlice.IsPayingZeroCoordinationFee);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SuccessWithAliceUpdateIntraRoundAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		using Key key = new();
		var ownershipProof = WabiSabiFactory.CreateOwnershipProof(key, round.Id);
		var coin = WabiSabiFactory.CreateCoin(key);

		// Make sure an Alice have already been registered with the same input.
		var preAlice = WabiSabiFactory.CreateAlice(coin, WabiSabiFactory.CreateOwnershipProof(key), round);
		round.Alices.Add(preAlice);

		var rpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);
		using Arena arena = await ArenaBuilder.From(cfg).With(rpc).CreateAndStartAsync(round);

		var arenaClient = WabiSabiFactory.CreateArenaClient(arena);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arenaClient.RegisterInputAsync(round.Id, coin.Outpoint, ownershipProof, CancellationToken.None).ConfigureAwait(false));
		Assert.Equal(WabiSabiProtocolErrorCode.AliceAlreadyRegistered, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}
}

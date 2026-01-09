using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Coordinator;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.Coordinator.WabiSabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class RegisterInputToBlameRoundTests
{
	[Fact]
	public async Task InputNotWhitelistedAsync()
	{
		WabiSabiConfig cfg = new();
		using Key key = new();
		var coin = WabiSabiFactory.CreateCoin(key);
		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(coin);

		var round = WabiSabiFactory.CreateRound(cfg);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		using Arena arena = await ArenaBuilder.From(cfg).With(mockRpc).CreateAndStartAsync(round, blameRound);

		var req = WabiSabiFactory.CreateInputRegistrationRequest(round: blameRound, key, coin.Outpoint);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterInputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.InputNotWhitelisted, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputWhitelistedAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round, blameRound);

		var req = WabiSabiFactory.CreateInputRegistrationRequest(prevout: alice.Coin.Outpoint, round: blameRound);

		var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await arena.RegisterInputAsync(req, CancellationToken.None));
		if (ex is WabiSabiProtocolException wspex)
		{
			Assert.NotEqual(WabiSabiProtocolErrorCode.InputNotWhitelisted, wspex.ErrorCode);
		}

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InputWhitelistedButBannedAsync()
	{
		WabiSabiConfig cfg = WabiSabiFactory.CreateWabiSabiConfig();
		var round = WabiSabiFactory.CreateRound(cfg);

		using Key key = new();
		var alice = WabiSabiFactory.CreateAlice(key, round);
		var bannedCoin = alice.Coin.Outpoint;

		round.Alices.Add(alice);
		Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(alice.Coin);

		Prison prison = WabiSabiFactory.CreatePrison();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync(round, blameRound);

		prison.FailedToConfirm(bannedCoin, alice.Coin.Amount, round.Id);

		var req = WabiSabiFactory.CreateInputRegistrationRequest(key: key, round: blameRound, prevout: bannedCoin);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterInputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}
}

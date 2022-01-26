using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class RegisterInputToBlameRoundTests
{
	[Fact]
	public async Task InputNotWhitelistedAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round, blameRound);
		using Key key = new();
		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient();

		var req = WabiSabiFactory.CreateInputRegistrationRequest(round: blameRound);
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
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		using Key key = new();
		var alice = WabiSabiFactory.CreateAlice(key, round);
		var bannedCoin = alice.Coin.Outpoint;

		round.Alices.Add(alice);
		Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
		var mockRpc = WabiSabiFactory.CreatePreconfiguredRpcClient(alice.Coin);

		Prison prison = new();
		using Arena arena = await ArenaBuilder.From(cfg, mockRpc, prison).CreateAndStartAsync(round, blameRound);

		prison.Punish(bannedCoin, Punishment.Banned, uint256.Zero);

		var req = WabiSabiFactory.CreateInputRegistrationRequest(key: key, round: blameRound, prevout: bannedCoin);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterInputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}
}

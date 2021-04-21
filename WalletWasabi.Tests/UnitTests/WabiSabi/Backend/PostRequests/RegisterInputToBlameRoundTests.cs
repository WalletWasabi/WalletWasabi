using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests
{
	public class RegisterInputToBlameRoundTests
	{
		[Fact]
		public async Task InputNotWhitelistedAsync()
		{
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice());
			Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round, blameRound);
			using Key key = new();

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var req = WabiSabiFactory.CreateInputRegistrationRequest(round: blameRound);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputNotWhitelisted, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputWhitelistedAsync()
		{
			var outpoint = BitcoinFactory.CreateOutPoint();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice(prevout: outpoint));
			Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round, blameRound);

			await using ArenaRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc());
			var req = WabiSabiFactory.CreateInputRegistrationRequest(prevout: outpoint, round: blameRound);

			var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await handler.RegisterInputAsync(req));
			if (ex is WabiSabiProtocolException wspex)
			{
				Assert.NotEqual(WabiSabiProtocolErrorCode.InputNotWhitelisted, wspex.ErrorCode);
			}

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputWhitelistedButBannedAsync()
		{
			Prison prison = new();
			var outpoint = BitcoinFactory.CreateOutPoint();
			prison.Punish(outpoint, Punishment.Banned, Guid.NewGuid());
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice(prevout: outpoint));
			Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round, blameRound);

			await using ArenaRequestHandler handler = new(cfg, prison, arena, WabiSabiFactory.CreateMockRpc());
			var req = WabiSabiFactory.CreateInputRegistrationRequest(round: blameRound, prevout: outpoint);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}

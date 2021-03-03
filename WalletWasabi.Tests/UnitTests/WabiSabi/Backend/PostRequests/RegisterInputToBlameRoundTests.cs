using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
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

			await using PostRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc(key));
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(blameRound);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputNotWhitelisted, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task InputWhitelistedAsync()
		{
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice(pair));
			Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round, blameRound);

			await using PostRequestHandler handler = new(cfg, new(), arena, WabiSabiFactory.CreateMockRpc());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(pair, blameRound);

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
			var pair = WabiSabiFactory.CreateInputRoundSignaturePair();
			prison.Punish(pair.Input, Punishment.Banned, Guid.NewGuid());
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Alices.Add(WabiSabiFactory.CreateAlice(pair));
			Round blameRound = WabiSabiFactory.CreateBlameRound(round, cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, round, blameRound);

			await using PostRequestHandler handler = new(cfg, prison, arena, WabiSabiFactory.CreateMockRpc());
			var req = WabiSabiFactory.CreateInputsRegistrationRequest(pair, blameRound);
			var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await handler.RegisterInputAsync(req));
			Assert.Equal(WabiSabiProtocolErrorCode.InputBanned, ex.ErrorCode);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}

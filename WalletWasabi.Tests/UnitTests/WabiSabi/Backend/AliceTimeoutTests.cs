using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend
{
	public class AliceTimeoutTests
	{
		[Fact]
		public async Task AliceTimesoutAsync()
		{
			// Alice times out when its deadline is reached.
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			MockRpcClient rpc = new();
			using Key key = new();

			rpc.OnGetTxOutAsync = (_, _, _) => new()
			{
				Confirmations = 1,
				ScriptPubKeyType = "witness_v0_keyhash",
				TxOut = new TxOut(Money.Coins(1), key.PubKey.GetSegwitAddress(Network.Main))
			};

			var req = WabiSabiFactory.CreateInputsRegistrationRequest(key, round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, rpc);
			var minAliceDeadline = DateTimeOffset.UtcNow + cfg.ConnectionConfirmationTimeout * 0.9;
			await handler.RegisterInputAsync(req);

			var alice = Assert.Single(round.Alices);
			alice.Deadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Empty(round.Alices);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceDoesntTimesoutInConnectionConfirmationAsync()
		{
			// Alice does not time out when it's not input registration anymore,
			// even though the deadline is reached.
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			round.Phase = Phase.ConnectionConfirmation;
			var alice = WabiSabiFactory.CreateAlice();
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			Assert.Single(round.Alices);
			DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			alice.Deadline = preDeadline;
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Single(round.Alices);
			Assert.Equal(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AliceDeadlineUpdatedAsync()
		{
			// Alice's deadline is updated by connection confirmation.
			WabiSabiConfig cfg = new();
			var round = WabiSabiFactory.CreateRound(cfg);
			var alice = WabiSabiFactory.CreateAlice();
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(round);

			var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
			await using PostRequestHandler handler = new(cfg, new Prison(), arena, new MockRpcClient());

			Assert.Single(round.Alices);
			DateTimeOffset preDeadline = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(1);
			alice.Deadline = preDeadline;
			handler.ConfirmConnection(req);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Single(round.Alices);
			Assert.NotEqual(preDeadline, alice.Deadline);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}

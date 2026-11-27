using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.RPC;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Coordinator.Services;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.DoSPrevention;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Coordinator.Services;

/// <summary>
/// Tests for <see cref="CoinBanningService"/>.
/// </summary>
public class CoinBanningServiceTests
{
	/// <summary>
	/// Tests that descendants of banned outpoints are also banned with proper shorter ban times.
	/// </summary>
	[Fact(Timeout = 20_000)]
	public async Task BanDescendantsTestAsync()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var network = Network.Main;

		var config = new WabiSabiConfig();
		var dosConfig = config.GetDoSConfiguration();
		var prison = new Prison([], Channel.CreateUnbounded<Offender>().Writer);

		// The ban time is halved twice below, so make sure the result is not clamped to zero by the minimum time in prison.
		Assert.True(dosConfig.MinTimeForCheating / 4 >= dosConfig.MinTimeInPrison);

		// Ban a coin.
		var bannedCoin = new OutPoint(RandomUtils.GetUInt256(), 0);
		prison.CheatingDetected(bannedCoin, roundId: uint256.One);

		// A transaction that spends the banned coin and creates two outputs.
		var spendingTx = CreateTransaction(network, bannedCoin, outputCount: 2);

		// A transaction in the same block that spends an output of the transaction above.
		var grandchildTx = CreateTransaction(network, new OutPoint(spendingTx, 0), outputCount: 1);

		// A transaction in the same block that spends a coin that is not banned.
		var unrelatedTx = CreateTransaction(network, new OutPoint(RandomUtils.GetUInt256(), 0), outputCount: 1);

		// Create a block containing a transaction spending a banned UTXO, its descendant (it must come after its parent) and another transaction that is fine.
		var block = network.Consensus.ConsensusFactory.CreateBlock();
		block.AddTransaction(CreateCoinbase(network));
		block.AddTransaction(unrelatedTx);
		block.AddTransaction(spendingTx);
		block.AddTransaction(grandchildTx);
		var blockHash = block.GetHash();

		var mockRpcClient = new MockRpcClient
		{
			Network = network,
			OnGetVerboseBlockAsync = hash =>
			{
				Assert.Equal(blockHash, hash);
				return Task.FromResult(new GetBlockRPCResponse { Block = block, Height = 100 });
			}
		};

		// Start the coin banning service and present the block hash.
		{
			ZmqEvent[] events = [ZmqEvent.SubscriptionStarts, new ZmqEvent(blockHash, Sequence: 1)];
			await RunServiceAsync(config, mockRpcClient, prison, events, cancellationToken);
		}

		// Assert bans now.
		{
			var now = DateTimeOffset.UtcNow;

			// The banned coin was given the "cheating" ban.
			var parentPeriod = prison.GetBanTimePeriod(bannedCoin, dosConfig);
			Assert.Equal(dosConfig.MinTimeForCheating, parentPeriod.Duration);
			Assert.True(prison.IsBanned(bannedCoin, dosConfig, now));

			// Every output of the spending transaction inherits the ban with the same start time but with the half of the duration.
			for (int i = 0; i < spendingTx.Outputs.Count; i++)
			{
				var outPoint = new OutPoint(spendingTx, i);
				var period = prison.GetBanTimePeriod(outPoint, dosConfig);

				Assert.Equal(parentPeriod.StartTime, period.StartTime);
				Assert.Equal(parentPeriod.Duration / 2, period.Duration);
				Assert.True(prison.IsBanned(outPoint, dosConfig, now), $"Output {i} should be banned.");
			}

			// The descendant in the same block inherits the ban too, with the same start time but with the quarter of the duration.
			for (int i = 0; i < grandchildTx.Outputs.Count; i++)
			{
				var outPoint = new OutPoint(grandchildTx, i);
				var period = prison.GetBanTimePeriod(outPoint, dosConfig);

				Assert.Equal(parentPeriod.StartTime, period.StartTime);
				Assert.Equal(parentPeriod.Duration / 4, period.Duration);
				Assert.True(prison.IsBanned(outPoint, dosConfig, now), $"Output {i} of the descendant should be banned.");
			}

			// Sanity check that an unrelated transaction's outpoint is not banned.
			var unrelatedPeriod = prison.GetBanTimePeriod(new OutPoint(unrelatedTx, 0), dosConfig);
			Assert.Equal(TimeSpan.Zero, unrelatedPeriod.Duration);
			Assert.False(prison.IsBanned(new OutPoint(unrelatedTx, 0), dosConfig, now));
		}
	}

	/// <summary>
	/// Expired ban of a coin must not be inherited.
	/// </summary>
	[Fact(Timeout = 20_000)]
	public async Task ExpiredBanTestAsync()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var network = Network.Main;
		var config = new WabiSabiConfig();
		var dosConfig = config.GetDoSConfiguration();

		// A "cheating" ban that ended two days ago.
		var expiredCoin = new OutPoint(RandomUtils.GetUInt256(), 0);
		var prison = new Prison([], Channel.CreateUnbounded<Offender>().Writer);
		prison.CheatingDetected(expiredCoin, roundId: uint256.One, DateTimeOffset.UtcNow - TimeSpan.FromDays(2));
		Assert.False(prison.IsBanned(expiredCoin, dosConfig, DateTimeOffset.UtcNow));

		var spendingTx = CreateTransaction(network, expiredCoin, outputCount: 2);
		var block = CreateBlock(network, spendingTx);
		var blockHash = block.GetHash();

		var rpc = new MockRpcClient
		{
			Network = network,
			OnGetVerboseBlockAsync = hash =>
			{
				Assert.Equal(blockHash, hash);
				return Task.FromResult(new GetBlockRPCResponse { Block = block, Height = 100 });
			}
		};

		// Start the coin banning service and present the block hash.
		{
			ZmqEvent[] events = [ZmqEvent.SubscriptionStarts, new ZmqEvent(blockHash, Sequence: 1)];
			await RunServiceAsync(config, rpc, prison, events, cancellationToken);
		}

		// Assert bans now. None should be given.
		{
			var now = DateTimeOffset.UtcNow;

			for (int i = 0; i < spendingTx.Outputs.Count; i++)
			{
				var outPoint = new OutPoint(spendingTx, i);
				Assert.Equal(TimeSpan.Zero, prison.GetBanTimePeriod(outPoint, dosConfig).Duration);
				Assert.False(prison.IsBanned(outPoint, dosConfig, now), $"Output {i} should not be banned.");
			}
		}
	}

	private static async Task RunServiceAsync(WabiSabiConfig config, MockRpcClient rpc, Prison prison, ZmqEvent[] events, CancellationToken cancellationToken)
	{
		// ZMQ is abstracted away by this channel.
		var channel = Channel.CreateUnbounded<ZmqEvent>();
		using var service = new CoinBanningService(config, rpc, prison, channel, new ZmqMockService());

		await service.StartAsync(cancellationToken);

		foreach (var ev in events)
		{
			await channel.Writer.WriteAsync(ev, cancellationToken);
		}

		// Completing the channel ends ExecuteAsync once everything queued was processed.
		channel.Writer.Complete();
		await service.ExecuteTask!.WaitAsync(cancellationToken);
	}

	private static Block CreateBlock(Network network, params Transaction[] transactions)
	{
		var block = network.Consensus.ConsensusFactory.CreateBlock();
		block.AddTransaction(CreateCoinbase(network));

		foreach (var tx in transactions)
		{
			block.AddTransaction(tx);
		}

		return block;
	}

	private static Transaction CreateCoinbase(Network network)
	{
		var tx = network.CreateTransaction();
		tx.Inputs.Add(new TxIn(new OutPoint(uint256.Zero, uint.MaxValue)));

		using var key = new Key();
		tx.Outputs.Add(Money.Coins(50m), key.GetScriptPubKey(ScriptPubKeyType.Segwit));
		return tx;
	}

	private static Transaction CreateTransaction(Network network, OutPoint input, int outputCount)
	{
		var tx = network.CreateTransaction();
		tx.Inputs.Add(new TxIn(input));

		for (int i = 0; i < outputCount; i++)
		{
			using var key = new Key();
			tx.Outputs.Add(Money.Coins(0.1m), key.GetScriptPubKey(ScriptPubKeyType.Segwit));
		}

		return tx;
	}

	private class ZmqMockService : IHostedService
	{
		public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}

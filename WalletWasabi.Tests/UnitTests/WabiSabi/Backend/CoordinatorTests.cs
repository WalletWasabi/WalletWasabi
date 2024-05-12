using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class CoordinatorTests
{
	[Fact]
	public async Task CanLiveAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);
		CoordinatorParameters coordinatorParameters = new(workDir);
		using WabiSabiCoordinator coordinator = CreateWabiSabiCoordinator(coordinatorParameters);
		await coordinator.StartAsync(CancellationToken.None);
		await coordinator.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task CanCancelAsync()
	{
		var workDir = Common.GetWorkDir();
		await IoHelpers.TryDeleteDirectoryAsync(workDir);
		CoordinatorParameters coordinatorParameters = new(workDir);

		using WabiSabiCoordinator coordinator = CreateWabiSabiCoordinator(coordinatorParameters);
		using CancellationTokenSource cts = new();
		cts.Cancel();
		await coordinator.StartAsync(cts.Token);
		await coordinator.StopAsync(CancellationToken.None);

		using WabiSabiCoordinator coordinator2 = CreateWabiSabiCoordinator(coordinatorParameters);
		using CancellationTokenSource cts2 = new();
		await coordinator2.StartAsync(cts2.Token);
		cts2.Cancel();
		await coordinator2.StopAsync(CancellationToken.None);

		using WabiSabiCoordinator coordinator3 = CreateWabiSabiCoordinator(coordinatorParameters);
		using CancellationTokenSource cts3 = new();
		var t = coordinator3.StartAsync(cts3.Token);
		cts3.Cancel();
		await t;
		await coordinator3.StopAsync(CancellationToken.None);

		using WabiSabiCoordinator coordinator4 = CreateWabiSabiCoordinator(coordinatorParameters);
		await coordinator4.StartAsync(CancellationToken.None);
		using CancellationTokenSource cts4 = new();
		cts4.Cancel();
		await coordinator4.StopAsync(cts4.Token);

		using WabiSabiCoordinator coordinator5 = CreateWabiSabiCoordinator(coordinatorParameters);
		await coordinator5.StartAsync(CancellationToken.None);
		using CancellationTokenSource cts5 = new();
		t = coordinator5.StopAsync(cts5.Token);
		cts5.Cancel();
		await t;
	}

	[Fact]
	public void BanDoubleSpendersTest()
	{
		var workDir = Common.GetWorkDir();
		CoordinatorParameters coordinatorParameters = new(workDir);
		WabiSabiConfig cfg = coordinatorParameters.RuntimeCoordinatorConfig;
		DoSConfiguration dosConfig = cfg.GetDoSConfiguration() with { MinTimeInPrison = TimeSpan.Zero };
		var coinJoinIdStore = new InMemoryCoinJoinIdStore();
		var mockRpcClient = new MockRpcClient { Network = Network.Main };
		using WabiSabiCoordinator coordinator = new(coordinatorParameters, mockRpcClient, coinJoinIdStore, new CoinJoinScriptStore(), new MockHttpClientFactory());

		// Receive a tx that is not spending coins registered in any round.
		{
			var tx1 = CreateTransaction(Money.Coins(0.1m));
			coordinator.BanDoubleSpenders(this, tx1);
			var isOutputBanned = coordinator.Warden.Prison.IsBanned(new OutPoint(tx1, 0), dosConfig, DateTimeOffset.UtcNow);
			Assert.False(isOutputBanned); // Not banned.
		}

		Transaction tx2;

		// Receive a tx that is spending coins registered in a round.
		{
			var round = WabiSabiFactory.CreateRound(cfg);
			using Key key = new();
			Alice alice = WabiSabiFactory.CreateAlice(key: key, round: round);

			// Register our coin..
			round.CoinjoinState = round.AddInput(alice.Coin, alice.OwnershipProof, WabiSabiFactory.CreateCommitmentData(round.Id));
			round.SetPhase(Phase.ConnectionConfirmation);
			coordinator.Arena.Rounds.Add(round);

			// .. spend it also in another transaction paying less fee rate than the coinjoin
			tx2 = CreateTransaction(Money.Coins(0.999999m), alice.Coin.Outpoint); // spends almost the full bitcoin.
			mockRpcClient.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { TxOut = alice.Coin.TxOut };
			coordinator.BanDoubleSpenders(this, tx2);
			var isOutputBanned = coordinator.Warden.Prison.IsBanned(new OutPoint(tx2, 0), dosConfig, DateTimeOffset.UtcNow);
			Assert.True(isOutputBanned); // Banned.
			Assert.DoesNotContain(round.Id, coordinator.Arena.DisruptedRounds);

			// .. spend it also in another transaction (tx2).
			tx2.Outputs[0].Value = Money.Coins(0.1m);
			mockRpcClient.OnGetTxOutAsync = (_, _, _) => new GetTxOutResponse { TxOut = alice.Coin.TxOut };
			coordinator.BanDoubleSpenders(this, tx2);
			isOutputBanned = coordinator.Warden.Prison.IsBanned(new OutPoint(tx2, 0), dosConfig, DateTimeOffset.UtcNow);
			Assert.True(isOutputBanned); // Banned.
			Assert.Contains(round.Id, coordinator.Arena.DisruptedRounds);
		}

		// Receive a tx that is spending coins registered in a round but the tx is a Wasabi coinjoin
		{
			tx2.Outputs[0].ScriptPubKey = BitcoinFactory.CreateScript(); // Make it a completely different tx.

			// Make the transaction look like a Wasabi coinjoin tx.
			Assert.True(coinJoinIdStore.TryAdd(tx2.GetHash()));

			// Attempt to ban Wasabi coinjoin tx.
			coordinator.BanDoubleSpenders(this, tx2);

			var isOutputBanned = coordinator.Warden.Prison.IsBanned(new OutPoint(tx2, 0), dosConfig, DateTimeOffset.UtcNow);
			Assert.False(isOutputBanned); // Not banned.
		}
	}

	private static Transaction CreateTransaction(Money amount, OutPoint? outPoint = default)
	{
		var tx = Network.RegTest.CreateTransaction();
		tx.Version = 1;
		tx.LockTime = LockTime.Zero;
		tx.Inputs.Add(outPoint ?? BitcoinFactory.CreateOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		using Key key = new();
		tx.Outputs.Add(amount, key.GetScriptPubKey(ScriptPubKeyType.Segwit));
		return tx;
	}

	private static IRPCClient NewMockRpcClient()
	{
		var mockRpcClient = new MockRpcClient { Network = Network.Main };
		mockRpcClient.OnEstimateSmartFeeAsync = (_, _) =>
			Task.FromResult(new EstimateSmartFeeResponse { Blocks = 5, FeeRate = new FeeRate(100m) });
		mockRpcClient.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo { MemPoolMinFee = 0.00001000 });
		return mockRpcClient;
	}

	private static WabiSabiCoordinator CreateWabiSabiCoordinator(CoordinatorParameters coordinatorParameters)
		=> new(coordinatorParameters, NewMockRpcClient(), new CoinJoinIdStore(), new CoinJoinScriptStore(), new MockHttpClientFactory());
}

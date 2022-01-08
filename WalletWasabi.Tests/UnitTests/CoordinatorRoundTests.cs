using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.CoinJoin.Coordinator.Banning;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

public class CoordinatorRoundTests
{
	[Fact]
	public async Task TryOptimizeFeesTestAsync()
	{
		var rpc = new MockRpcClient();
		rpc.Network = Network.Main;
		rpc.OnEstimateSmartFeeAsync = (confTarget, estMode) => Task.FromResult(new EstimateSmartFeeResponse
		{
			Blocks = 1,
			FeeRate = new FeeRate(10m)
		});

		var roundConfig = new CoordinatorRoundConfig();
		var utxoReferee = new UtxoReferee(Network.Main, "./", rpc, roundConfig);
		var confirmationTarget = 12;
		var round = new CoordinatorRound(rpc, utxoReferee, roundConfig, adjustedConfirmationTarget: confirmationTarget, confirmationTarget, roundConfig.ConfirmationTargetReductionRate, TimeSpan.FromSeconds(roundConfig.InputRegistrationTimeout));

		static OutPoint GetRandomOutPoint() => new(RandomUtils.GetUInt256(), 0);
		var tx = Network.Main.CreateTransaction();
		tx.Version = 1;
		tx.LockTime = LockTime.Zero;
		tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		using var key = new Key();
		tx.Outputs.Add(Money.Coins(1.9995m), key.PubKey.ScriptPubKey);

		// Under normal circunstances
		{
			rpc.OnGetMempoolInfoAsync = () => Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000 // 1 s/b (default value)
			});

			var tx0 = tx.Clone();
			var spentCoins = tx0.Inputs.Select(x => new Coin(x.PrevOut, new TxOut(Money.Coins(1), new Key().PubKey.ScriptPubKey)));
			var txFeeBeforeOptimization = tx0.GetFee(spentCoins.ToArray());
			await round.TryOptimizeFeesAsync(tx0, spentCoins);
			var txFeeAfterOptimization = tx0.GetFee(spentCoins.ToArray());
			Assert.True(txFeeAfterOptimization < txFeeBeforeOptimization);
		}

		// Under heavy mempool pressure
		{
			rpc.OnGetMempoolInfoAsync = () => Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = 0.0028 // 280 s/b
			});

			var tx1 = tx.Clone();
			var spentCoins = tx1.Inputs.Select(x => new Coin(x.PrevOut, new TxOut(Money.Coins(1), new Key().PubKey.ScriptPubKey)));
			var txFeeBeforeOptimization = tx1.GetFee(spentCoins.ToArray());
			await round.TryOptimizeFeesAsync(tx1, spentCoins);
			var txFeeAfterOptimization = tx1.GetFee(spentCoins.ToArray());

			Assert.Equal(txFeeAfterOptimization, txFeeBeforeOptimization);
		}
	}

	[Fact]
	public async Task CalculateFeesTestAsync()
	{
		const double DefaultMinMempoolFee = 0.00001000; // 1 s/b (default value)
		const double HighestMinMempoolFee = 0.00200000; // 200 s/b
		const int InputSizeInBytes = 67;
		const int OutputSizeInBytes = 33;

		var rpc = new MockRpcClient();
		rpc.Network = Network.Main;
		rpc.OnEstimateSmartFeeAsync = (confTarget, estMode) => Task.FromResult(new EstimateSmartFeeResponse
		{
			Blocks = 1,
			FeeRate = new FeeRate(10m)
		});

		{
			rpc.OnGetMempoolInfoAsync = () => Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = DefaultMinMempoolFee
			});

			var (feePerInputs, feePerOutputs) = await CoordinatorRound.CalculateFeesAsync(rpc, 12);

			var defaultMinMempoolFeeRate = new FeeRate(Money.Coins((decimal)DefaultMinMempoolFee));
			Assert.True(feePerInputs > defaultMinMempoolFeeRate.GetFee(InputSizeInBytes));
			Assert.True(feePerOutputs > defaultMinMempoolFeeRate.GetFee(OutputSizeInBytes));
		}

		{
			rpc.OnGetMempoolInfoAsync = () => Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = HighestMinMempoolFee
			});

			var (feePerInputs, feePerOutputs) = await CoordinatorRound.CalculateFeesAsync(rpc, 12);

			var highestMinMempoolFeeRate = new FeeRate(Money.Coins((decimal)HighestMinMempoolFee * 1.5m));
			Assert.Equal(highestMinMempoolFeeRate.GetFee(InputSizeInBytes), feePerInputs);
			Assert.Equal(highestMinMempoolFeeRate.GetFee(OutputSizeInBytes), feePerOutputs);
		}
	}
}

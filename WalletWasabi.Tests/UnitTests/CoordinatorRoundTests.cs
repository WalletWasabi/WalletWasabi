using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.CoinJoin.Coordinator.Banning;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests
{
	public class CoordinatorRoundTests
	{
		[Fact]
		public async Task SsAsync()
		{
			var rpc = new MockRpcClient();
			var roundConfig = new CoordinatorRoundConfig();
			var utxoReferee = new UtxoReferee(Network.Main, "./", rpc, roundConfig);
			var confirmationTarget = 12;
			var round = new CoordinatorRound(rpc, utxoReferee, roundConfig, adjustedConfirmationTarget: confirmationTarget, confirmationTarget, roundConfig.ConfirmationTargetReductionRate);

			static OutPoint GetRandomOutPoint() => new OutPoint(RandomUtils.GetUInt256(), 0);
			var tx = Network.Main.CreateTransaction();
			tx.Version = 1;
			tx.LockTime = LockTime.Zero;
			tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
			tx.Inputs.Add(GetRandomOutPoint(), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
			tx.Outputs.Add(Money.Coins(1.9995m), new Key().ScriptPubKey);

			// Under normal circunstances
			{
				rpc.OnGetMempoolInfoAsync = () => Task.FromResult(new MemPoolInfo
				{
					MemPoolMinFee = 0.00001000 // 1 s/b (default value)
				});

				var tx0 = tx.Clone();
				var spentCoins = tx0.Inputs.Select(x => new Coin(x.PrevOut, new TxOut(Money.Coins(1), new Key().ScriptPubKey)));
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
				var spentCoins = tx1.Inputs.Select(x => new Coin(x.PrevOut, new TxOut(Money.Coins(1), new Key().ScriptPubKey)));
				var txFeeBeforeOptimization = tx1.GetFee(spentCoins.ToArray());
				await round.TryOptimizeFeesAsync(tx1, spentCoins);
				var txFeeAfterOptimization = tx1.GetFee(spentCoins.ToArray());

				Assert.True(txFeeAfterOptimization == txFeeBeforeOptimization);
			}
		}

		[Fact]
		public async Task XxAsync()
		{
			const double DefaultMinMemPoolFee = 0.00001000; // 1 s/b (default value)
			const double HighestMinMemPoolFee = 0.00200000; // 200 s/b
			const int InputSizeInBytes = 67;
			const int OutputSizeInBytes = 33;

			var rpc = new MockRpcClient();
			{
				rpc.OnGetMempoolInfoAsync = () => Task.FromResult(new MemPoolInfo
				{
					MemPoolMinFee = DefaultMinMemPoolFee
				});

				var (feePerInputs, feePerOutputs) = await CoordinatorRound.CalculateFeesAsync(rpc, 12);

				var defaultMinMemPoolFeeRate = new FeeRate(Money.Coins((decimal)DefaultMinMemPoolFee));
				Assert.True(feePerInputs > defaultMinMemPoolFeeRate.GetFee(InputSizeInBytes));
				Assert.True(feePerOutputs > defaultMinMemPoolFeeRate.GetFee(OutputSizeInBytes));
			}

			{
				rpc.OnGetMempoolInfoAsync = () => Task.FromResult(new MemPoolInfo
				{
					MemPoolMinFee = HighestMinMemPoolFee
				});

				var (feePerInputs, feePerOutputs) = await CoordinatorRound.CalculateFeesAsync(rpc, 12);

				var higherMinMemPoolFeeRate = new FeeRate(Money.Coins((decimal)HighestMinMemPoolFee));
				Assert.True(feePerInputs == higherMinMemPoolFeeRate.GetFee(InputSizeInBytes));
				Assert.True(feePerOutputs == higherMinMemPoolFeeRate.GetFee(OutputSizeInBytes));
			}
		}
	}
}

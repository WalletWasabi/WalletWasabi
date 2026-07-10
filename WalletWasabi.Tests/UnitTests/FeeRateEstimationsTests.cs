using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.FeeRateEstimation;
using Xunit;

namespace WalletWasabi.Tests.UnitTests;

/// <summary>
/// Tests for <see cref="FeeRateEstimations"/>.
/// </summary>
public class FeeRateEstimationsTests
{
	[Fact]
	public void OrdersByTarget()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 3, new FeeRate( 20M) },
			{ 2, new FeeRate( 102M) },
			{ 19,new FeeRate(  1M) },
			{ 20,new FeeRate(  1M) }
		};

		var allFee = new FeeRateEstimations(estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
		Assert.Equal(estimations[3], allFee.Estimations[3]);
		Assert.Equal(estimations[19], allFee.Estimations[36]);
	}

	[Fact]
	public void HandlesDuplicate()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 2, new FeeRate(20M) },
			{ 3, new FeeRate(20M) }
		};

		var allFee = new FeeRateEstimations(estimations);
		Assert.Single(allFee.Estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
	}

	[Fact]
	public void HandlesOne()
	{
		// If there's no 2, this'll be 2.
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 1, new FeeRate(20M) }
		};

		var allFees = new FeeRateEstimations(estimations);
		Assert.Single(allFees.Estimations);
		Assert.Equal(estimations[1], allFees.Estimations[2]);

		// If there's 2, 1 is dismissed.
		estimations = new Dictionary<int, FeeRate>
		{
			{ 1, new FeeRate(20M) },
			{ 2, new FeeRate(21M) }
		};

		allFees = new FeeRateEstimations(estimations);
		Assert.Single(allFees.Estimations);
		Assert.Equal(estimations[2], allFees.Estimations[2]);
	}

	[Fact]
	public void EndOfTheRange()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 1007, new FeeRate(20M) }
		};

		var allFees = new FeeRateEstimations(estimations);
		var est = Assert.Single(allFees.Estimations);
		Assert.Equal(1008, est.Key);
	}

	[Fact]
	public void HandlesInconsistentData()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 2, new FeeRate(20M) },
			{ 3, new FeeRate(21M) }
		};

		var allFee = new FeeRateEstimations(estimations);
		Assert.Single(allFee.Estimations);
		Assert.Equal(estimations[2], allFee.Estimations[2]);

		estimations = new Dictionary<int, FeeRate>
		{
			{ 18, new FeeRate(1000M) },
			{ 3, new FeeRate(21M) },
			{ 2, new FeeRate(20M) },
			{ 100, new FeeRate(100M) },
			{ 6, new FeeRate(4M) },
		};

		allFee = new FeeRateEstimations(estimations);
		Assert.Equal(2, allFee.Estimations.Count);
		Assert.Equal(estimations[2], allFee.Estimations[2]);
		Assert.Equal(estimations[6], allFee.Estimations[6]);
	}

	[Fact]
	public async Task RpcNotEnoughEstimationsAsync()
	{
		var mockRpc = new MockRpcClient();
		mockRpc.Network = Network.Main;
		mockRpc.OnGetBlockchainInfoAsync = () =>
			Task.FromResult(new BlockchainInfo
			{
				Blocks = 100,
				Headers = 100
			});
		mockRpc.OnGetPeersInfoAsync = () =>
			Task.FromResult(Array.Empty<PeerInfo>());
		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000 // 1 s/b (default value)
			});
		mockRpc.OnEstimateSmartFeeAsync = (_, _) =>
			throw new NoEstimationException(1);

		await Assert.ThrowsAsync<NoEstimationException>(async () => await mockRpc.EstimateAllFeeAsync());
	}

	[Fact]
	public async Task RpcFailuresAsync()
	{
		var mockRpc = new MockRpcClient();
		mockRpc.Network = Network.Main;
		mockRpc.OnGetBlockchainInfoAsync = () =>
			Task.FromException<BlockchainInfo>(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-GetBlockchainInfo", null));

		mockRpc.OnEstimateSmartFeeAsync = (_, _) =>
			Task.FromException<EstimateSmartFeeResponse>(new RPCException(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, "Error-EstimateSmartFee", null));

		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = 0.00001000 // 1 s/b (default value)
			});

		mockRpc.OnUptimeAsync = () => Task.FromResult(TimeSpan.FromDays(500));
		var ex = await Assert.ThrowsAsync<RPCException>(async () => await mockRpc.EstimateAllFeeAsync());
		Assert.Equal(RPCErrorCode.RPC_CLIENT_NOT_CONNECTED, ex.RPCCode);
		Assert.Equal("Error-EstimateSmartFee", ex.Message);
	}

	[Fact]
	public async Task ToleratesRpcFailuresAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient();
		mockRpc.Network = Network.Main;
		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 100m)),
				3 => Task.FromException<EstimateSmartFeeResponse>(new RPCException(RPCErrorCode.RPC_INTERNAL_ERROR, "Error", null)),
				5 => Task.FromResult(FeeRateResponse(5, 89m)),
				6 => Task.FromResult(FeeRateResponse(6, 75m)),
				8 => Task.FromResult(FeeRateResponse(8, 70m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		var allFee = await mockRpc.EstimateAllFeeAsync();
		Assert.Equal(2, allFee.Estimations.Count);
		Assert.False(allFee.Estimations.ContainsKey(3));
		Assert.False(allFee.Estimations.ContainsKey(5));
		Assert.False(allFee.Estimations.ContainsKey(8));
	}

	[Fact]
	public async Task AccurateEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);
		mockRpc.Network = Network.Main;
		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 99m)),
				3 => Task.FromResult(FeeRateResponse(3, 99m)),
				5 => Task.FromResult(FeeRateResponse(5, 89m)),
				6 => Task.FromResult(FeeRateResponse(6, 75m)),
				8 => Task.FromResult(FeeRateResponse(8, 30m)),
				11 => Task.FromResult(FeeRateResponse(11, 30m)),
				13 => Task.FromResult(FeeRateResponse(13, 30m)),
				15 => Task.FromResult(FeeRateResponse(15, 30m)),
				1008 => Task.FromResult(FeeRateResponse(1008, 31m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		var allFee = await mockRpc.EstimateAllFeeAsync();
		Assert.Equal(3, allFee.Estimations.Count);
		Assert.Equal(99, allFee.Estimations[2].SatoshiPerByte);
		Assert.Equal(75, allFee.Estimations[6].SatoshiPerByte);
		Assert.Equal(31, allFee.Estimations[1008].SatoshiPerByte);
	}

	[Fact]
	public async Task WorksWithBitcoinCoreEstimationsAsync()
	{
		var mockRpc = CreateAndConfigureRpcClient(hasPeersInfo: true);

		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 120m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		// Do not throw exception
		await mockRpc.EstimateAllFeeAsync();
	}

	[Fact]
	public void WildEstimations()
	{
		var estimations = new Dictionary<int, FeeRate>
		{
			{ 2,new FeeRate(102m) }, // 20m
			{ 3,new FeeRate(20m) }, // 30m
			{ 6,new FeeRate(10m) }, // 1h
			{ 18,new FeeRate(1m) } // 3h
		};

		var allFee = new FeeRateEstimations(estimations);

		Assert.Equal(7, allFee.WildEstimations.Count);
		Assert.Equal(new FeeRate(102m), allFee.WildEstimations[0].feeRate); // 20m
		Assert.Equal(new FeeRate(20m), allFee.WildEstimations[1].feeRate); // 30m
		Assert.Equal(new FeeRate(16.666m), allFee.WildEstimations[2].feeRate); // 40m
		Assert.Equal(new FeeRate(13.333m), allFee.WildEstimations[3].feeRate); // 50m
		Assert.Equal(new FeeRate(10m), allFee.WildEstimations[4].feeRate); // 1h
		Assert.Equal(new FeeRate(5.5m), allFee.WildEstimations[5].feeRate); // 2h
		Assert.Equal(new FeeRate(1m), allFee.WildEstimations[6].feeRate); // 3h

		Assert.Equal(TimeSpan.FromMinutes(10), allFee.EstimateConfirmationTime(new FeeRate(200m)));
		Assert.Equal(TimeSpan.FromMinutes(20), allFee.EstimateConfirmationTime(new FeeRate(102.1m)));
		Assert.Equal(TimeSpan.FromMinutes(20), allFee.EstimateConfirmationTime(new FeeRate(102m)));
		Assert.Equal(TimeSpan.FromMinutes(30), allFee.EstimateConfirmationTime(new FeeRate(101.9m)));
		Assert.Equal(TimeSpan.FromMinutes(30), allFee.EstimateConfirmationTime(new FeeRate(50m)));
		Assert.Equal(TimeSpan.FromMinutes(30), allFee.EstimateConfirmationTime(new FeeRate(20m)));
		Assert.Equal(TimeSpan.FromMinutes(40), allFee.EstimateConfirmationTime(new FeeRate(19m)));
		Assert.Equal(TimeSpan.FromMinutes(60), allFee.EstimateConfirmationTime(new FeeRate(11m)));
		Assert.Equal(TimeSpan.FromMinutes(60), allFee.EstimateConfirmationTime(new FeeRate(10m)));
		Assert.Equal(TimeSpan.FromHours(2), allFee.EstimateConfirmationTime(new FeeRate(9m)));
		Assert.Equal(TimeSpan.FromHours(3), allFee.EstimateConfirmationTime(new FeeRate(3m)));
		Assert.Equal(TimeSpan.FromHours(3), allFee.EstimateConfirmationTime(new FeeRate(1m)));
		Assert.Equal(TimeSpan.FromHours(3), allFee.EstimateConfirmationTime(new FeeRate(0.1m)));
	}

	private static MockRpcClient CreateAndConfigureRpcClient(bool isSynchronized = true, bool hasPeersInfo = false, double memPoolMinFee = 0.00001000)
	{
		var mockRpc = new MockRpcClient();
		mockRpc.OnGetBlockchainInfoAsync = () =>
			Task.FromResult(new BlockchainInfo
			{
				Blocks = isSynchronized ? 100_000UL : 89_765UL,
				Headers = 100_000L
			});
		mockRpc.OnGetPeersInfoAsync = () =>
			Task.FromResult(hasPeersInfo
				? new[] { new PeerInfo() }
				: []);
		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				MemPoolMinFee = memPoolMinFee, // 1 s/b (default value)
				Histogram = []
			});

		mockRpc.OnUptimeAsync = () => Task.FromResult(TimeSpan.FromDays(500));
		return mockRpc;
	}

	private static EstimateSmartFeeResponse FeeRateResponse(int target, decimal feeRate) =>
		new()
		{
			Blocks = target,
			FeeRate = new FeeRate(feeRate)
		};

	/// <summary>
	/// Tests that mempool data is used to adjust fee estimates even when mempool-derived
	/// targets (1, 2, 3, 4...) don't exactly match Bitcoin Core targets (2, 3, 6, 18, 36...).
	///
	/// Scenario: Mempool has 4MB of transactions at high fee rates (100+ sat/vB).
	/// Bitcoin Core underestimates target 6 at only 10 sat/vB.
	/// The algorithm should use the closest mempool estimate (target 4) to push
	/// the target 6 estimate higher.
	/// </summary>
	[Fact]
	public async Task MempoolDataUsedForNonExactTargetMatches()
	{
		var mockRpc = new MockRpcClient();
		mockRpc.Network = Network.Main;
		mockRpc.OnGetBlockchainInfoAsync = () =>
			Task.FromResult(new BlockchainInfo
			{
				Blocks = 100_000UL,
				Headers = 100_000L
			});
		mockRpc.OnGetPeersInfoAsync = () => Task.FromResult(new[] { new PeerInfo() });
		mockRpc.OnUptimeAsync = () => Task.FromResult(TimeSpan.FromDays(10)); // >2 hours, so mempool path is used

		// Mempool with 4MB of high-fee transactions:
		// - 1MB at 200 sat/vB (group 200)
		// - 1MB at 150 sat/vB (group 150)
		// - 1MB at 120 sat/vB (group 120)
		// - 1MB at 100 sat/vB (group 100)
		// This means: target 1 needs 200 sat/vB, target 2 needs 150, target 3 needs 120, target 4 needs 100
		// For target 6, the mempool says we'd need ~100 sat/vB to get in within 6 blocks
		mockRpc.OnGetMempoolInfoAsync = () =>
			Task.FromResult(new MemPoolInfo
			{
				Size = 4000, // 4000 transactions
				MemPoolMinFee = 0.00001000, // 1 sat/vB default
				Histogram =
				[
					new FeeRateGroup { Group = 200, Sizes = 1_000_000, Count = 1000, From = new FeeRate(200m), To = new FeeRate(300m) },
					new FeeRateGroup { Group = 150, Sizes = 1_000_000, Count = 1000, From = new FeeRate(150m), To = new FeeRate(200m) },
					new FeeRateGroup { Group = 120, Sizes = 1_000_000, Count = 1000, From = new FeeRate(120m), To = new FeeRate(150m) },
					new FeeRateGroup { Group = 100, Sizes = 1_000_000, Count = 1000, From = new FeeRate(100m), To = new FeeRate(120m) },
				]
			});

		// Bitcoin Core severely underestimates fees (maybe node just started or low traffic period)
		mockRpc.OnEstimateSmartFeeAsync = (target, _) =>
			target switch
			{
				2 => Task.FromResult(FeeRateResponse(2, 20m)),   // Core says 20 sat/vB for 2 blocks
				3 => Task.FromResult(FeeRateResponse(3, 15m)),   // Core says 15 sat/vB for 3 blocks
				6 => Task.FromResult(FeeRateResponse(6, 10m)),   // Core says 10 sat/vB for 6 blocks - WAY too low!
				18 => Task.FromResult(FeeRateResponse(18, 5m)),
				36 => Task.FromResult(FeeRateResponse(36, 3m)),
				72 => Task.FromResult(FeeRateResponse(72, 2m)),
				144 => Task.FromResult(FeeRateResponse(144, 1m)),
				432 => Task.FromResult(FeeRateResponse(432, 1m)),
				1008 => Task.FromResult(FeeRateResponse(1008, 1m)),
				_ => Task.FromException<EstimateSmartFeeResponse>(new NoEstimationException(0))
			};

		var result = await mockRpc.EstimateAllFeeAsync();

		// The mempool clearly shows that to get confirmed in 6 blocks, you need ~100 sat/vB
		// (since 4MB of 100+ sat/vB transactions are ahead of you).
		// The algorithm should use the closest mempool estimate (target 4 at 100 sat/vB)
		// to adjust the target 6 estimate.
		var target6Estimate = result.Estimations[6];

		// The mempool data should push the estimate to ~100 sat/vB (from target 4's estimate)
		Assert.True(
			target6Estimate.SatoshiPerByte >= 100m,
			$"Expected target 6 fee rate >= 100 sat/vB based on mempool congestion, " +
			$"but got {target6Estimate.SatoshiPerByte} sat/vB.");
	}
}

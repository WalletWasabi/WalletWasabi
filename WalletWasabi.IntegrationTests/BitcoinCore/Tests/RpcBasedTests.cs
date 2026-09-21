using NBitcoin;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.IntegrationTests.Infrastructure;
using Xunit;

namespace WalletWasabi.IntegrationTests.BitcoinCore.Tests;

public class RpcBasedTests
{
	[Fact]
	public async Task AllFeeEstimateAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		try
		{
			var rpc = coreNode.RpcClient;
			var estimations = await rpc.EstimateAllFeeAsync();
			Assert.Equal(9, estimations.Estimations.Count);
			Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
			Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
		}
		finally
		{
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task FeeEstimationCanCancelAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		try
		{
			var rpc = coreNode.RpcClient;
			using CancellationTokenSource cts = new(System.TimeSpan.Zero);
			await Assert.ThrowsAsync<TaskCanceledException>(async () => await rpc.EstimateAllFeeAsync(cts.Token));
		}
		finally
		{
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task VerboseBlockInfoAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		try
		{
			var rpc = coreNode.RpcClient;
			var response = await rpc.GetVerboseBlockAsync(coreNode.Network.GenesisHash);
			Assert.NotNull(response.Block);
			Assert.NotNull(response.PrevOuts);
			var prevOutList = Assert.IsType<List<PrevOutInfo>>(response.PrevOuts[0]);
			var prevOutInfo = Assert.Single(prevOutList);
			Assert.Null(prevOutInfo);
		}
		finally
		{
			await coreNode.TryStopAsync();
		}
	}

	[Fact]
	public async Task GetRawTransactionsAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync();
		try
		{
			var rpc = coreNode.RpcClient;
			var txs = await rpc.GetRawTransactionsAsync(new[] { CreateRandomUint256(), CreateRandomUint256() }, CancellationToken.None);
			Assert.Empty(txs);

			await rpc.CreateWalletAsync("wallet");
			await rpc.GenerateAsync(101);
			var txid = await rpc.SendToAddressAsync(CreateRandomAddress(Network.RegTest), Money.Coins(1));

			txs = await rpc.GetRawTransactionsAsync(new[] { txid }, CancellationToken.None);
			Assert.Single(txs);

			List<Task<uint256>> txidTasks1 = new();
			for (int i = 0; i < 2; i++)
			{
				var txid2 = rpc.SendToAddressAsync(CreateRandomAddress(Network.RegTest), Money.Coins(1));
				txidTasks1.Add(txid2);
			}

			var txids1 = await Task.WhenAll(txidTasks1);

			txs = await rpc.GetRawTransactionsAsync(txids1, CancellationToken.None);
			Assert.Equal(2, txs.Count());

			List<Task<uint256>> txidTasks2 = new();
			for (int i = 0; i < 20; i++)
			{
				var txid2 = rpc.SendToAddressAsync(CreateRandomAddress(Network.RegTest), Money.Coins(1));
				txidTasks2.Add(txid2);
			}

			var txids2 = await Task.WhenAll(txidTasks2);
			txs = await rpc.GetRawTransactionsAsync(txids2, CancellationToken.None);
			Assert.Equal(20, txs.Count());
		}
		finally
		{
			await coreNode.TryStopAsync();
		}
	}

	private static uint256 CreateRandomUint256()
	{
		return new uint256(RandomUtils.GetBytes(32));
	}

	private static BitcoinAddress CreateRandomAddress(Network network)
	{
		using var key = new Key();
		return key.PubKey.GetAddress(ScriptPubKeyType.Segwit, network);
	}
}

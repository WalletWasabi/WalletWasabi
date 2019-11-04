using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.KeyManagement;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class RpcBasedTests
	{
		[Fact]
		public async Task AllFeeEstimateAsync()
		{
			var coreNode = await TestNodeBuilder.CreateAsync();
			try
			{
				var rpc = coreNode.RpcClient;
				var estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: true);
				Assert.Equal(WalletWasabi.Helpers.Constants.OneDayConfirmationTarget, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Conservative, estimations.Type);
				estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: true);
				Assert.Equal(145, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Economical, estimations.Type);
				estimations = await rpc.EstimateAllFeeAsync(EstimateSmartFeeMode.Economical, simulateIfRegTest: true, tolerateBitcoinCoreBrainfuck: false);
				Assert.Equal(145, estimations.Estimations.Count);
				Assert.True(estimations.Estimations.First().Key < estimations.Estimations.Last().Key);
				Assert.True(estimations.Estimations.First().Value > estimations.Estimations.Last().Value);
				Assert.Equal(EstimateSmartFeeMode.Economical, estimations.Type);
			}
			finally
			{
				await coreNode.TryStopAsync(deleteDataDir: true);
			}
		}
	}
}

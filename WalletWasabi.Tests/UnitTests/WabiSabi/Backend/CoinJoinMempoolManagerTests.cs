using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class CoinJoinMempoolManagerTests
{
	[Fact]
	public async Task CoinJoinVisibleTestAsync()
	{
		var coreNode = await TestNodeBuilder.CreateAsync(nameof(CoinJoinMempoolManagerTests), nameof(CoinJoinVisibleTestAsync));
		using HostedServices services = new();
		services.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(2), coreNode.RpcClient, coreNode.P2pNode), "Mempool Mirror");
		try
		{
			var rpc = coreNode.RpcClient;
			var network = rpc.Network;
			await services.StartAllAsync();
			var mempoolInstance = services.Get<MempoolMirror>();

			var walletName = "RandomWalletName";
			await rpc.CreateWalletAsync(walletName);

			var spendAmount = new Money(0.0004m, MoneyUnit.BTC);
			await rpc.GenerateAsync(101);

			var rpcMempoolBeforeSend = await rpc.GetRawMempoolAsync();
			var localMempoolBeforeSend = mempoolInstance.GetMempoolHashes();
			Assert.Equal(rpcMempoolBeforeSend.Length, localMempoolBeforeSend.Count);

			await rpc.SendToAddressAsync(BitcoinFactory.CreateBitcoinAddress(network), spendAmount);
			while ((await rpc.GetRawMempoolAsync()).Length == 0)
			{
				await Task.Delay(50);
			}

			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
			var localMempoolAfterSend = mempoolInstance.GetMempoolHashes();
			var coinjoinTx = localMempoolAfterSend.First();

			CoinJoinIdStore coinjoinIdStore = new();
			coinjoinIdStore.TryAdd(coinjoinTx);

			using CoinJoinMempoolManager coinJoinMempoolManager = new(coinjoinIdStore, mempoolInstance);
			await mempoolInstance.TriggerAndWaitRoundAsync(CancellationToken.None);

			Assert.Equal(coinjoinTx, coinJoinMempoolManager.CoinJoinIds.Single());

			await rpc.GenerateAsync(1);
			while ((await rpc.GetRawMempoolAsync()).Length != 0)
			{
				await Task.Delay(50);
			}
			await mempoolInstance.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

			Assert.Empty(coinJoinMempoolManager.CoinJoinIds);
		}
		finally
		{
			await services.StopAllAsync();
			await coreNode.TryStopAsync();
		}
	}
}

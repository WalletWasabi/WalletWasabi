using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
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

	private static IRPCClient NewMockRpcClient()
	{
		var mockRpcClient = new Mock<IRPCClient>();
		mockRpcClient.Setup(rpc => rpc.Network).Returns(Network.Main);
		mockRpcClient.Setup(rpc => rpc.EstimateSmartFeeAsync(It.IsAny<int>(), It.IsAny<EstimateSmartFeeMode>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new EstimateSmartFeeResponse { Blocks = 5, FeeRate = new FeeRate(100m) });
		mockRpcClient.Setup(rpc => rpc.GetMempoolInfoAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(new MemPoolInfo { MemPoolMinFee = 0.00001000 });
		return mockRpcClient.Object;
	}

	private static WabiSabiCoordinator CreateWabiSabiCoordinator(CoordinatorParameters coordinatorParameters)
		=> new(coordinatorParameters, NewMockRpcClient(), new CoinJoinIdStore(), new CoinJoinScriptStore(), new Mock<IHttpClientFactory>(MockBehavior.Strict).Object);
}

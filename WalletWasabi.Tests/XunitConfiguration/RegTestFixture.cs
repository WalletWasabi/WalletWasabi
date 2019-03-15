using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using System;
using System.Threading.Tasks;
using WalletWasabi.Backend;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Tests.NodeBuilding;

namespace WalletWasabi.Tests.XunitConfiguration
{
	public class RegTestFixture : IDisposable
	{
		public string BackendEndPoint { get; internal set; }

		public IWebHost BackendHost { get; internal set; }

		public NodeBuilder BackendNodeBuilder { get; internal set; }

		public CoreNode BackendRegTestNode { get; internal set; }

		public RegTestFixture()
		{
			BackendNodeBuilder = NodeBuilder.CreateAsync(nameof(RegTestFixture)).GetAwaiter().GetResult();
			BackendNodeBuilder.CreateNodeAsync().GetAwaiter().GetResult();
			BackendNodeBuilder.StartAllAsync().GetAwaiter().GetResult();
			BackendRegTestNode = BackendNodeBuilder.Nodes[0];

			var rpc = BackendRegTestNode.CreateRpcClient();

			var config = new Config(rpc.Network, rpc.Authentication);

			var roundConfig = new CcjRoundConfig(Money.Coins(0.1m), 144, 0.1m, 100, 120, 60, 60, 60, 1, 24, true, 11);

			Global.InitializeAsync(config, roundConfig, rpc).GetAwaiter().GetResult();

			BackendEndPoint = $"http://localhost:{new Random().Next(37130, 38000)}/";
			BackendHost = WebHost.CreateDefaultBuilder()
					.UseStartup<Startup>()
					.UseUrls(BackendEndPoint)
					.Build();

			var hostInitializationTask = BackendHost.RunAsync();
			Logger.LogInfo<SharedFixture>($"Started Backend webhost: {BackendEndPoint}");

			var delayTask = Task.Delay(3000);
			Task.WaitAny(delayTask, hostInitializationTask); // Wait for server to initialize (Without this OSX CI will fail)
		}

		public void Dispose()
		{
			// Cleanup tests...

			BackendHost?.StopAsync();
			BackendHost?.Dispose();
			BackendRegTestNode?.Kill(cleanFolder: true);
			BackendNodeBuilder?.Dispose();
		}
	}
}

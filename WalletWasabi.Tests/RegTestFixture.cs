using WalletWasabi.Backend;
using WalletWasabi.Logging;
using WalletWasabi.Tests.NodeBuilding;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Tests
{
	public class RegTestFixture : IDisposable
	{
		public string BackendEndPoint { get; internal set; }

		public IWebHost BackendHost { get; internal set; }

		public NodeBuilder BackendNodeBuilder { get; internal set; }

		public CoreNode BackendRegTestNode { get; internal set; }

		public RegTestFixture()
		{
			BackendNodeBuilder = NodeBuilder.CreateAsync().GetAwaiter().GetResult();
			BackendNodeBuilder.CreateNodeAsync().GetAwaiter().GetResult();
			BackendNodeBuilder.StartAllAsync().GetAwaiter().GetResult();
			BackendRegTestNode = BackendNodeBuilder.Nodes[0];

			var rpc = BackendRegTestNode.CreateRpcClient();

			var authString = rpc.Authentication.Split(':');
			Global.InitializeAsync(rpc.Network, authString[0], authString[1], rpc).GetAwaiter().GetResult();

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

using MagicalCryptoWallet.Backend;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Tests.NodeBuilding;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
    public class BackendTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		private CoreNode SharedRegTestNode { get; }
		private RPCClient SharedRegTestRpc { get; }

		public BackendTests(SharedFixture fixture)
		{
			SharedFixture = fixture;

			SharedFixture.BackEndNodeBuilder = NodeBuilder.Create();
			SharedFixture.BackEndNodeBuilder.CreateNode();
			SharedFixture.BackEndNodeBuilder.StartAll();
			SharedRegTestNode = SharedFixture.BackEndNodeBuilder.Nodes[0];
			SharedRegTestNode.Generate(101);
			SharedRegTestRpc = SharedRegTestNode.CreateRPCClient();

			var authString = SharedRegTestRpc.Authentication.Split(':');
			Global.InitializeAsync(SharedRegTestRpc.Network, authString[0], authString[1], SharedRegTestRpc).GetAwaiter().GetResult();

			SharedFixture.BackendEndPoint = $"http://localhost:{new Random().Next(37130, 38000)}/";
			SharedFixture.BackendHost = WebHost.CreateDefaultBuilder()
				.UseStartup<Startup>()
				.UseUrls(SharedFixture.BackendEndPoint)
				.Build();
			SharedFixture.BackendHost.RunAsync();
			Logger.LogInfo<SharedFixture>($"Started Backend webhost: {SharedFixture.BackendEndPoint}");
		}

		[Fact]
		public async Task FilterInitializationAsync()
		{
			await AssertFiltersInitializedAsync();
		}

		private async Task AssertFiltersInitializedAsync()
		{
			var firstHash = SharedRegTestRpc.GetBlockHash(0);
			while (true)
			{
				using (var request = await new HttpClient().GetAsync(SharedFixture.BackendEndPoint + "api/v1/btc/Blockchain/filters/" + firstHash))
				{
					var content = await request.Content.ReadAsStringAsync();
					var filterCount = content.Split(',').Count();
					if (filterCount >= 101)
					{
						break;
					}
					else
					{
						await Task.Delay(100);
					}
				}
			}
		}
	}
}

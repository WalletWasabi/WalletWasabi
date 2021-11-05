using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Rpc;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests
{
	[Collection("RegTest collection")]
	public class RpcServerTests : IDisposable
	{
		private RegTestFixture RegTestFixture { get; }
		private JsonRpcServer RpcServer { get; }

		public RpcServerTests(RegTestFixture regTestFixture)
		{
			RegTestFixture = regTestFixture;
		}

		[Fact]
		public async Task GetStatusTestAsync()
		{
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}

		//private async Task StartRpcServerAsync(TerminateService terminateService, CancellationToken cancel)
		//{
		//	(string password, IRPCClient rpc, Network network, Coordinator coordinator, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

		//	using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
		//	WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);

		//	try
		//	{
		//		synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 1000);
		//		var jsonRpcServerConfig = new JsonRpcServerConfiguration(Config);
		//		if (jsonRpcServerConfig.IsEnabled)
		//		{
		//			RpcServer = new JsonRpcServer(global, jsonRpcServerConfig, terminateService);
		//			//try
		//			//{
		//			await RpcServer.StartAsync(cancel).ConfigureAwait(false);
		//			//}
		//			//catch (HttpListenerException e)
		//			//{
		//			//	Logger.LogWarning($"Failed to start {nameof(JsonRpcServer)} with error: {e.Message}.");
		//			//	RpcServer = null;
		//			//}
		//		}
		//	}
		//	finally
		//	{
		//		if (synchronizer is { })
		//		{
		//			await synchronizer.StopAsync();
		//		}
		//	}
		//}
	}
}

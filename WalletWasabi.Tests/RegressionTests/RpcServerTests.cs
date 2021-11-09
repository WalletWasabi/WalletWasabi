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
using NBitcoin.Protocol;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Helpers;
using Moq;

namespace WalletWasabi.Tests.RegressionTests
{
	[Collection("RegTest collection")]
	public class RpcServerTests
	{
		private RegTestFixture RegTestFixture { get; }
		private JsonRpcServer RpcServer { get; set; }

		public RpcServerTests(RegTestFixture regTestFixture)
		{
			RegTestFixture = regTestFixture;
		}

		[Fact]
		public async Task GetStatusTestAsync()
		{
			(string password, IRPCClient rpc, Network network, _, ServiceConfiguration serviceConfiguration, BitcoinStore bitcoinStore, Backend.Global global) = await Common.InitializeTestEnvironmentAsync(RegTestFixture, 1);

			bitcoinStore.IndexStore.NewFilter += Common.Wallet_NewFilterProcessed;
			// Create the services.
			// 1. Create connection service.
			NodesGroup nodes = new(global.Config.Network, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Add(await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync());

			// 2. Create mempool service.

			Node node = await RegTestFixture.BackendRegTestNode.CreateNewP2pNodeAsync();
			node.Behaviors.Add(bitcoinStore.CreateUntrustedP2pBehavior());

			// 3. Create wasabi synchronizer service.
			using HttpClientFactory httpClientFactory = new(torEndPoint: null, backendUriGetter: () => new Uri(RegTestFixture.BackendEndPoint));
			WasabiSynchronizer synchronizer = new(bitcoinStore, httpClientFactory);
			HybridFeeProvider feeProvider = new(synchronizer, null);

			// 4. Create key manager service.
			var keyManager = KeyManager.CreateNew(out _, password, network);

			// 5. Create wallet service.
			var workDir = Helpers.Common.GetWorkDir();

			CachedBlockProvider blockProvider = new(
				new P2pBlockProvider(nodes, null, httpClientFactory, serviceConfiguration, network),
				bitcoinStore.BlockRepository);

			WalletManager walletManager = new(network, workDir, new WalletDirectories(network, workDir));
			walletManager.RegisterServices(bitcoinStore, synchronizer, serviceConfiguration, feeProvider, blockProvider);

			var wallet = await walletManager.AddAndStartWalletAsync(keyManager);

			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);

			var terminateService = new TerminateService(async () => { });
			var hostedServices = new HostedServices();
			var configMock = new Mock<IJsonRpcServerConfiguration>();
			var config = configMock.Object;

			RpcServer = new JsonRpcServer(config, terminateService,
				new WasabiJsonRpcService(terminateService)
				{
					BitcoinStore = bitcoinStore,
					HostedServices = hostedServices,
					Network = network,
					Synchronizer = synchronizer,
					TransactionBroadcaster = broadcaster,
					WalletManager = walletManager
				});
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

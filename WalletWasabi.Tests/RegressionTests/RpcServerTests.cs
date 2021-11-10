using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Shouldly;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Rpc;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

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

			Interlocked.Exchange(ref Common.FiltersProcessedByWalletCount, 0);
			nodes.Connect(); // Start connection service.
			node.VersionHandshake(); // Start mempool service.
			synchronizer.Start(requestInterval: TimeSpan.FromSeconds(3), 10000); // Start wasabi synchronizer service.
			await feeProvider.StartAsync(CancellationToken.None);

			// Wait until the filter our previous transaction is present.
			var blockCount = await rpc.GetBlockCountAsync();
			await Common.WaitForFiltersToBeProcessedAsync(TimeSpan.FromSeconds(120), blockCount);
			var wallet = await walletManager.AddAndStartWalletAsync(keyManager);

			TransactionBroadcaster broadcaster = new(network, bitcoinStore, httpClientFactory, walletManager);
			broadcaster.Initialize(nodes, rpc);

			var terminateService = new TerminateService(async () => { });
			var hostedServices = new HostedServices();
			var configMock = new Mock<IJsonRpcServerConfiguration>();
			var config = configMock.Object;

			configMock.Setup(a => a.IsEnabled).Returns(true);
			configMock.Setup(a => a.Prefixes).Returns(new[] { "http://localhost:53851/" });

			hostedServices.Register<P2pNetwork>(new P2pNetwork(network, new IPEndPoint(IPAddress.Loopback, Constants.DefaultRegTestBitcoinP2pPort), null, workDir, bitcoinStore), "P2pnetworks");

			var rpcService = new WasabiJsonRpcService(terminateService)
			{
				BitcoinStore = bitcoinStore,
				HostedServices = hostedServices,
				Network = network,
				Synchronizer = synchronizer,
				TransactionBroadcaster = broadcaster,
				WalletManager = walletManager
			};

			RpcServer = new JsonRpcServer(config, terminateService, rpcService);

			await RpcServer.StartAsync(CancellationToken.None).ConfigureAwait(false);

			using var client = new HttpClient();
			var response = await client.PostAsync(config.Prefixes.First(), new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"getstatus\"}"));
			response.StatusCode.ShouldBe(HttpStatusCode.OK);
			var responseString = await response.Content.ReadAsStringAsync();
			var responseObject = JsonConvert.DeserializeObject<JsonRpcResponse>(responseString);
		}
	}
}

using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi;

namespace WalletWasabi.Backend
{
	public class Global
	{
		public Global(string dataDir)
		{
			DataDir = dataDir ?? EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Backend"));
			HostedServices = new();
		}

		public string DataDir { get; }

		public IRPCClient RpcClient { get; private set; }

		public P2pNode P2pNode { get; private set; }

		public HostedServices HostedServices { get; }

		public IndexBuilderService IndexBuilderService { get; private set; }

		public Coordinator Coordinator { get; private set; }

		public Config Config { get; private set; }

		public CoordinatorRoundConfig RoundConfig { get; private set; }

		public async Task InitializeAsync(Config config, CoordinatorRoundConfig roundConfig, IRPCClient rpc, CancellationToken cancel)
		{
			Config = Guard.NotNull(nameof(config), config);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);

			// Make sure RPC works.
			await AssertRpcNodeFullyInitializedAsync();

			// Make sure P2P works.
			await InitializeP2pAsync(config.Network, config.GetBitcoinP2pEndPoint(), cancel);

			HostedServices.Register<MempoolMirror>(new MempoolMirror(TimeSpan.FromSeconds(21), RpcClient, P2pNode), "Full Node Mempool Mirror");

			CoordinatorParameters coordinatorParameters = new(DataDir);
			HostedServices.Register<WabiSabiCoordinator>(new WabiSabiCoordinator(coordinatorParameters, RpcClient), "WabiSabi Coordinator");

			if (roundConfig.FilePath is { })
			{
				HostedServices.Register<ConfigWatcher>(
					new ConfigWatcher(
						TimeSpan.FromSeconds(10), // Every 10 seconds check the config
						RoundConfig,
						() =>
						{
							try
							{
								Coordinator.RoundConfig.UpdateOrDefault(RoundConfig, toFile: false);

								Coordinator.AbortAllRoundsInInputRegistration($"{nameof(RoundConfig)} has changed.");
							}
							catch (Exception ex)
							{
								Logger.LogDebug(ex);
							}
						}),
					"Config Watcher");
			}

			await HostedServices.StartAllAsync(cancel);

			// Initialize index building
			var indexBuilderServiceDir = Path.Combine(DataDir, "IndexBuilderService");
			var indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{RpcClient.Network}.dat");
			var blockNotifier = HostedServices.Get<BlockNotifier>();
			IndexBuilderService = new(RpcClient, blockNotifier, indexFilePath);
			Coordinator = new(RpcClient.Network, blockNotifier, Path.Combine(DataDir, "CcjCoordinator"), RpcClient, roundConfig);
			IndexBuilderService.Synchronize();
			Logger.LogInfo($"{nameof(IndexBuilderService)} is successfully initialized and started synchronization.");

			await Coordinator.MakeSureInputregistrableRoundRunningAsync();
			Logger.LogInfo($"Chaumian CoinJoin Coordinator is successfully initialized and started '{Coordinator.GetRunningRounds().Count()}' new round(s).");
		}

		private async Task InitializeP2pAsync(Network network, EndPoint endPoint, CancellationToken cancel)
		{
			Guard.NotNull(nameof(network), network);
			Guard.NotNull(nameof(endPoint), endPoint);

			// We have to find it, because it's cloned by the node and not perfectly cloned (event handlers cannot be cloned.)
			P2pNode = new(network, endPoint, new(), $"/WasabiCoordinator:{Constants.BackendMajorVersion}/");
			await P2pNode.ConnectAsync(cancel).ConfigureAwait(false);
			HostedServices.Register<BlockNotifier>(new BlockNotifier(TimeSpan.FromSeconds(7), RpcClient, P2pNode), "Block Notifier");
		}

		private async Task AssertRpcNodeFullyInitializedAsync()
		{
			try
			{
				var blockchainInfo = await RpcClient.GetBlockchainInfoAsync();

				var blocks = blockchainInfo.Blocks;
				if (blocks == 0 && Config.Network != Network.RegTest)
				{
					throw new NotSupportedException($"{nameof(blocks)} == 0");
				}

				var headers = blockchainInfo.Headers;
				if (headers == 0 && Config.Network != Network.RegTest)
				{
					throw new NotSupportedException($"{nameof(headers)} == 0");
				}

				if (blocks != headers)
				{
					throw new NotSupportedException($"{Constants.BuiltinBitcoinNodeName} is not fully synchronized.");
				}

				Logger.LogInfo($"{Constants.BuiltinBitcoinNodeName} is fully synchronized.");

				if (Config.Network == Network.RegTest) // Make sure there's at least 101 block, if not generate it
				{
					if (blocks < 101)
					{
						var generateBlocksResponse = await RpcClient.GenerateAsync(101);
						if (generateBlocksResponse is null)
						{
							throw new NotSupportedException($"{Constants.BuiltinBitcoinNodeName} cannot generate blocks on the {Network.RegTest}.");
						}

						blockchainInfo = await RpcClient.GetBlockchainInfoAsync();
						blocks = blockchainInfo.Blocks;
						if (blocks == 0)
						{
							throw new NotSupportedException($"{nameof(blocks)} == 0");
						}
						Logger.LogInfo($"Generated 101 block on {Network.RegTest}. Number of blocks {blocks}.");
					}
				}
			}
			catch (WebException)
			{
				Logger.LogError($"{Constants.BuiltinBitcoinNodeName} is not running, or incorrect RPC credentials, or network is given in the config file: `{Config.FilePath}`.");
				throw;
			}
		}
	}
}

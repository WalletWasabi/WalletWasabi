using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.P2p;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Backend
{
	public class Global
	{
		public string DataDir { get; }

		public RPCClient RpcClient { get; private set; }

		public P2pNode P2pNode { get; private set; }

		public BlockNotifier BlockNotifier { get; private set; }

		public IndexBuilderService IndexBuilderService { get; private set; }

		public Coordinator Coordinator { get; private set; }

		public ConfigWatcher RoundConfigWatcher { get; private set; }

		public Config Config { get; private set; }

		public CoordinatorRoundConfig RoundConfig { get; private set; }

		public Global(string dataDir)
		{
			DataDir = dataDir ?? EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Backend"));
		}

		public Global() : this(null)
		{
		}

		public async Task InitializeAsync(Config config, CoordinatorRoundConfig roundConfig, RPCClient rpc, CancellationToken cancel)
		{
			Config = Guard.NotNull(nameof(config), config);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);

			// Make sure RPC works.
			await AssertRpcNodeFullyInitializedAsync();

			// Make sure P2P works.
			await InitializeP2pAsync(config.Network, config.GetBitcoinP2pEndPoint(), cancel);

			// Initialize index building
			var indexBuilderServiceDir = Path.Combine(DataDir, "IndexBuilderService");
			var indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{RpcClient.Network}.dat");
			var utxoSetFilePath = Path.Combine(indexBuilderServiceDir, $"UtxoSet{RpcClient.Network}.dat");
			IndexBuilderService = new IndexBuilderService(RpcClient, BlockNotifier, indexFilePath, utxoSetFilePath);
			Coordinator = new Coordinator(RpcClient.Network, BlockNotifier, Path.Combine(DataDir, "CcjCoordinator"), RpcClient, roundConfig);
			IndexBuilderService.Synchronize();
			Logger.LogInfo($"{nameof(IndexBuilderService)} is successfully initialized and started synchronization.");

			await Coordinator.MakeSureTwoRunningRoundsAsync();
			Logger.LogInfo("Chaumian CoinJoin Coordinator is successfully initialized and started two new rounds.");

			if (roundConfig.FilePath != null)
			{
				RoundConfigWatcher = new ConfigWatcher(
					TimeSpan.FromSeconds(10), // Every 10 seconds check the config
					RoundConfig,
					async () =>
					{
						try
						{
							await Coordinator.RoundConfig.UpdateOrDefaultAsync(RoundConfig, toFile: false);

							Coordinator.AbortAllRoundsInInputRegistration($"{nameof(RoundConfig)} has changed.");
						}
						catch (Exception ex)
						{
							Logger.LogDebug(ex);
						}
					});
				RoundConfigWatcher.Start();
				Logger.LogInfo($"{nameof(RoundConfigWatcher)} is successfully started.");
			}
		}

		private async Task InitializeP2pAsync(Network network, EndPoint endPoint, CancellationToken cancel)
		{
			Guard.NotNull(nameof(network), network);
			Guard.NotNull(nameof(endPoint), endPoint);

			// We have to find it, because it's cloned by the node and not perfectly cloned (event handlers cannot be cloned.)
			P2pNode = new P2pNode(network, endPoint, new MempoolService(), $"/WasabiCoordinator:{Constants.BackendMajorVersion.ToString()}/");
			await P2pNode.ConnectAsync(cancel).ConfigureAwait(false);
			BlockNotifier = new BlockNotifier(TimeSpan.FromSeconds(7), new RpcWrappedClient(RpcClient));
			P2pNode.BlockInv += (s, e) => BlockNotifier.TriggerRound();
			BlockNotifier.Start();
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
					throw new NotSupportedException("Bitcoin Core is not fully synchronized.");
				}

				Logger.LogInfo("Bitcoin Core is fully synchronized.");

				var estimateSmartFeeResponse = await RpcClient.TryEstimateSmartFeeAsync(2, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true);
				if (estimateSmartFeeResponse is null)
				{
					throw new NotSupportedException("Bitcoin Core cannot estimate network fees yet.");
				}

				Logger.LogInfo("Bitcoin Core fee estimation is working.");

				if (Config.Network == Network.RegTest) // Make sure there's at least 101 block, if not generate it
				{
					if (blocks < 101)
					{
						var generateBlocksResponse = await RpcClient.GenerateAsync(101);
						if (generateBlocksResponse is null)
						{
							throw new NotSupportedException($"Bitcoin Core cannot generate blocks on the {Network.RegTest}.");
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
				Logger.LogError($"Bitcoin Core is not running, or incorrect RPC credentials, or network is given in the config file: `{Config.FilePath}`.");
				throw;
			}
		}
	}
}

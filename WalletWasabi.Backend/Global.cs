using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.RPC;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;

namespace WalletWasabi.Backend
{
	public class Global
	{
		public static Global Instance { get; } = new Global();
		public string DataDir { get; }

		public RPCClient RpcClient { get; private set; }

		public Node LocalNode { get; private set; }

		public TrustedNodeNotifyingBehavior TrustedNodeNotifyingBehavior { get; private set; }

		public IndexBuilderService IndexBuilderService { get; private set; }

		public CcjCoordinator Coordinator { get; private set; }

		public ConfigWatcher RoundConfigWatcher { get; private set; }

		public Config Config { get; private set; }

		public CcjRoundConfig RoundConfig { get; private set; }

		public Global()
		{
			DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Backend"));
		}

		public async Task InitializeAsync(Config config, CcjRoundConfig roundConfig, RPCClient rpc)
		{
			Config = Guard.NotNull(nameof(config), config);
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);
			RpcClient = Guard.NotNull(nameof(rpc), rpc);

			// Make sure RPC works.
			await AssertRpcNodeFullyInitializedAsync();

			// Make sure P2P works.
			await InitializeP2pAsync(config.Network, config.GetBitcoinCoreEndPoint());

			// Initialize index building
			var indexBuilderServiceDir = Path.Combine(DataDir, nameof(IndexBuilderService));
			var indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{RpcClient.Network}.dat");
			var utxoSetFilePath = Path.Combine(indexBuilderServiceDir, $"UtxoSet{RpcClient.Network}.dat");
			IndexBuilderService = new IndexBuilderService(RpcClient, TrustedNodeNotifyingBehavior, indexFilePath, utxoSetFilePath);
			Coordinator = new CcjCoordinator(RpcClient.Network, TrustedNodeNotifyingBehavior, Path.Combine(DataDir, nameof(CcjCoordinator)), RpcClient, roundConfig);
			IndexBuilderService.Synchronize();
			Logger.LogInfo<IndexBuilderService>("IndexBuilderService is successfully initialized and started synchronization.");

			await Coordinator.MakeSureTwoRunningRoundsAsync();
			Logger.LogInfo<CcjCoordinator>("Chaumian CoinJoin Coordinator is successfully initialized and started two new rounds.");

			if (roundConfig.FilePath != null)
			{
				RoundConfigWatcher = new ConfigWatcher(RoundConfig);
				RoundConfigWatcher.Start(TimeSpan.FromSeconds(10), () =>
				{
					try
					{
						Coordinator.UpdateRoundConfig(RoundConfig);

						Coordinator.AbortAllRoundsInInputRegistration(nameof(ConfigWatcher), $"{nameof(RoundConfig)} has changed.");
					}
					catch (Exception ex)
					{
						Logger.LogDebug<ConfigWatcher>(ex);
					}

					return Task.CompletedTask;
				}); // Every 10 seconds check the config
				Logger.LogInfo<ConfigWatcher>($"{nameof(RoundConfigWatcher)} is successfully started.");
			}
		}

		public void DisconnectDisposeNullLocalNode()
		{
			if (LocalNode != null)
			{
				try
				{
					LocalNode?.Disconnect();
				}
				catch (Exception ex)
				{
					Logger.LogDebug<WalletService>(ex);
				}
				finally
				{
					try
					{
						LocalNode?.Dispose();
					}
					catch (Exception ex)
					{
						Logger.LogDebug<WalletService>(ex);
					}
					finally
					{
						LocalNode = null;
						Logger.LogInfo<WalletService>("Local Bitcoin node is disconnected.");
					}
				}
			}
		}

		private async Task InitializeP2pAsync(Network network, EndPoint endPoint)
		{
			Guard.NotNull(nameof(network), network);
			Guard.NotNull(nameof(endPoint), endPoint);

			using (var handshakeTimeout = new CancellationTokenSource())
			{
				handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
				var nodeConnectionParameters = new NodeConnectionParameters() {
					ConnectCancellation = handshakeTimeout.Token,
					IsRelay = true
				};

				nodeConnectionParameters.TemplateBehaviors.Add(new TrustedNodeNotifyingBehavior());
				var node = await Node.ConnectAsync(network, endPoint, nodeConnectionParameters);
				// We have to find it, because it's cloned by the node and not perfectly cloned (event handlers cannot be cloned.)
				TrustedNodeNotifyingBehavior = node.Behaviors.Find<TrustedNodeNotifyingBehavior>();
				try
				{
					Logger.LogInfo<Node>($"TCP Connection succeeded, handshaking...");
					node.VersionHandshake(Constants.LocalBackendNodeRequirements, handshakeTimeout.Token);
					var peerServices = node.PeerVersion.Services;

					if (!peerServices.HasFlag(NodeServices.Network) && !peerServices.HasFlag(NodeServices.NODE_NETWORK_LIMITED))
					{
						throw new InvalidOperationException($"Wasabi cannot use the local node because it does not provide blocks.");
					}

					Logger.LogInfo<Node>($"Handshake completed successfully.");

					if (!node.IsConnected)
					{
						throw new InvalidOperationException($"Wasabi could not complete the handshake with the local node and dropped the connection.{Environment.NewLine}" +
							$"Probably this is because the node does not support retrieving full blocks or segwit serialization.");
					}
					LocalNode = node;
				}
				catch (OperationCanceledException) when (handshakeTimeout.IsCancellationRequested)
				{
					Logger.LogWarning<Node>($"Wasabi could not complete the handshake with the local node. Probably Wasabi is not whitelisted by the node.{Environment.NewLine}" +
						$"Use \"whitebind\" in the node configuration. (Typically whitebind=127.0.0.1:8333 if Wasabi and the node are on the same machine and whitelist=1.2.3.4 if they are not.)");
					throw;
				}
			}
		}

		private async Task AssertRpcNodeFullyInitializedAsync()
		{
			try
			{
				var blockchainInfo = await RpcClient.GetBlockchainInfoAsync();

				var blocks = blockchainInfo.Blocks;
				if (blocks == 0 && Config.Network != Network.RegTest)
				{
					throw new NotSupportedException("blocks == 0");
				}

				var headers = blockchainInfo.Headers;
				if (headers == 0 && Config.Network != Network.RegTest)
				{
					throw new NotSupportedException("headers == 0");
				}

				if (blocks != headers)
				{
					throw new NotSupportedException("Bitcoin Core is not fully synchronized.");
				}

				Logger.LogInfo<RPCClient>("Bitcoin Core is fully synchronized.");

				var estimateSmartFeeResponse = await RpcClient.TryEstimateSmartFeeAsync(2, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, tryOtherFeeRates: true);
				if (estimateSmartFeeResponse is null)
				{
					throw new NotSupportedException("Bitcoin Core cannot estimate network fees yet.");
				}

				Logger.LogInfo<RPCClient>("Bitcoin Core fee estimation is working.");

				if (Config.Network == Network.RegTest) // Make sure there's at least 101 block, if not generate it
				{
					if (blocks < 101)
					{
						var generateBlocksResponse = await RpcClient.GenerateAsync(101);
						if (generateBlocksResponse is null)
						{
							throw new NotSupportedException("Bitcoin Core cannot generate blocks on the RegTest.");
						}

						blockchainInfo = await RpcClient.GetBlockchainInfoAsync();
						blocks = blockchainInfo.Blocks;
						if (blocks == 0)
						{
							throw new NotSupportedException("blocks == 0");
						}
						Logger.LogInfo<RPCClient>($"Generated 101 block on RegTest. Number of blocks {blocks}.");
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

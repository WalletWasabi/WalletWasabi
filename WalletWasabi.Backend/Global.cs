using NBitcoin;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Mempool;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.CoinJoin.Coordinator;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;

namespace WalletWasabi.Backend;

public class Global : IDisposable
{
	private bool _disposedValue;

	public Global(string dataDir, IRPCClient rpcClient, Config config)
	{
		DataDir = dataDir ?? EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Backend"));
		RpcClient = rpcClient;
		Config = config;
		HostedServices = new();

		// We have to find it, because it's cloned by the node and not perfectly cloned (event handlers cannot be cloned.)
		P2pNode = new(config.Network, config.GetBitcoinP2pEndPoint(), new(), $"/WasabiCoordinator:{Constants.BackendMajorVersion}/");
		HostedServices.Register<BlockNotifier>(() => new BlockNotifier(TimeSpan.FromSeconds(7), rpcClient, P2pNode), "Block Notifier");

		// Initialize index building
		string indexBuilderServiceDir = Path.Combine(DataDir, "IndexBuilderService");
		string indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{RpcClient.Network}.dat");
		IndexBuilderService = new(RpcClient, HostedServices.Get<BlockNotifier>(), indexFilePath);
	}

	public string DataDir { get; }

	public IRPCClient RpcClient { get; }

	public P2pNode P2pNode { get; }

	public HostedServices HostedServices { get; }

	public IndexBuilderService IndexBuilderService { get; }

	public Coordinator? Coordinator { get; private set; }

	public Config Config { get; }

	public CoordinatorRoundConfig? RoundConfig { get; private set; }
	public CoinJoinIdStore? CoinJoinIdStore { get; private set; }
	public WabiSabiCoordinator? WabiSabiCoordinator { get; private set; }

	public async Task InitializeAsync(CoordinatorRoundConfig roundConfig, CancellationToken cancel)
	{
		RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);

		// Make sure RPC works.
		await AssertRpcNodeFullyInitializedAsync(cancel);

		// Make sure P2P works.
		await P2pNode.ConnectAsync(cancel).ConfigureAwait(false);

		HostedServices.Register<MempoolMirror>(() => new MempoolMirror(TimeSpan.FromSeconds(21), RpcClient, P2pNode), "Full Node Mempool Mirror");

		var blockNotifier = HostedServices.Get<BlockNotifier>();

		CoordinatorParameters coordinatorParameters = new(DataDir);
		Coordinator = new(RpcClient.Network, blockNotifier, Path.Combine(DataDir, "CcjCoordinator"), RpcClient, roundConfig);
		Coordinator.CoinJoinBroadcasted += Coordinator_CoinJoinBroadcasted;

		var coordinator = Guard.NotNull(nameof(Coordinator), Coordinator);
		if (!string.IsNullOrWhiteSpace(roundConfig.FilePath))
		{
			HostedServices.Register<ConfigWatcher>(() =>
			   new ConfigWatcher(
				   TimeSpan.FromSeconds(10), // Every 10 seconds check the config
				   RoundConfig,
				   () =>
				   {
					   try
					   {
						   coordinator.RoundConfig.UpdateOrDefault(RoundConfig, toFile: false);

						   coordinator.AbortAllRoundsInInputRegistration($"{nameof(RoundConfig)} has changed.");
					   }
					   catch (Exception ex)
					   {
						   Logger.LogDebug(ex);
					   }
				   }),
				"Config Watcher");
		}

		CoinJoinIdStore = CoinJoinIdStore.Create(Coordinator.CoinJoinsFilePath, coordinatorParameters.CoinJoinIdStoreFilePath);

		WabiSabiCoordinator = new WabiSabiCoordinator(coordinatorParameters, RpcClient, CoinJoinIdStore);
		HostedServices.Register<WabiSabiCoordinator>(() => WabiSabiCoordinator, "WabiSabi Coordinator");
		HostedServices.Register<RoundBootstrapper>(() => new RoundBootstrapper(TimeSpan.FromMilliseconds(100), Coordinator), "Round Bootstrapper");

		await HostedServices.StartAllAsync(cancel);

		IndexBuilderService.Synchronize();
		Logger.LogInfo($"{nameof(IndexBuilderService)} is successfully initialized and started synchronization.");
	}

	private void Coordinator_CoinJoinBroadcasted(object? sender, Transaction transaction)
	{
		CoinJoinIdStore!.TryAdd(transaction.GetHash());
	}

	private async Task AssertRpcNodeFullyInitializedAsync(CancellationToken cancellationToken)
	{
		var rpcClient = Guard.NotNull(nameof(RpcClient), RpcClient);
		var config = Guard.NotNull(nameof(Config), Config);

		try
		{
			var blockchainInfo = await rpcClient.GetBlockchainInfoAsync(cancellationToken);

			var blocks = blockchainInfo.Blocks;
			if (blocks == 0 && config.Network != Network.RegTest)
			{
				throw new NotSupportedException($"{nameof(blocks)} == 0");
			}

			var headers = blockchainInfo.Headers;
			if (headers == 0 && config.Network != Network.RegTest)
			{
				throw new NotSupportedException($"{nameof(headers)} == 0");
			}

			if (blocks != headers)
			{
				throw new NotSupportedException($"{Constants.BuiltinBitcoinNodeName} is not fully synchronized.");
			}

			Logger.LogInfo($"{Constants.BuiltinBitcoinNodeName} is fully synchronized.");

			if (config.Network == Network.RegTest) // Make sure there's at least 101 block, if not generate it
			{
				if (blocks < 101)
				{
					var generateBlocksResponse = await rpcClient.GenerateAsync(101, cancellationToken);
					if (generateBlocksResponse is null)
					{
						throw new NotSupportedException($"{Constants.BuiltinBitcoinNodeName} cannot generate blocks on the {Network.RegTest}.");
					}

					blockchainInfo = await rpcClient.GetBlockchainInfoAsync(cancellationToken);
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
			Logger.LogError($"{Constants.BuiltinBitcoinNodeName} is not running, or incorrect RPC credentials, or network is given in the config file: `{config.FilePath}`.");
			throw;
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				if (Coordinator is { } coordinator)
				{
					coordinator.CoinJoinBroadcasted -= Coordinator_CoinJoinBroadcasted;
					coordinator.Dispose();
					Logger.LogInfo($"{nameof(coordinator)} is disposed.");
				}

				var stoppingTask = Task.Run(async () =>
				{
					if (IndexBuilderService is { } indexBuilderService)
					{
						await indexBuilderService.StopAsync().ConfigureAwait(false);
						Logger.LogInfo($"{nameof(indexBuilderService)} is stopped.");
					}

					if (HostedServices is { } hostedServices)
					{
						using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
						await hostedServices.StopAllAsync(cts.Token).ConfigureAwait(false);
						hostedServices.Dispose();
					}

					if (P2pNode is { } p2pNode)
					{
						await p2pNode.DisposeAsync().ConfigureAwait(false);
						Logger.LogInfo($"{nameof(p2pNode)} is disposed.");
					}
				});

				stoppingTask.GetAwaiter().GetResult();
			}

			_disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

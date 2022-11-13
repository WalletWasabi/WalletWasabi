using NBitcoin;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;

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
		HttpClient = new();

		CoordinatorParameters = new(DataDir);
		CoinJoinIdStore = CoinJoinIdStore.Create(Path.Combine(DataDir, "CcjCoordinator", $"CoinJoins{RpcClient.Network}.txt"), CoordinatorParameters.CoinJoinIdStoreFilePath);

		// We have to find it, because it's cloned by the node and not perfectly cloned (event handlers cannot be cloned.)
		P2pNode = new(config.Network, config.GetBitcoinP2pEndPoint(), new(), $"/WasabiCoordinator:{Constants.BackendMajorVersion}/");
		BlockNotifier blockNotifier = new BlockNotifier(TimeSpan.FromSeconds(7), rpcClient, P2pNode);
		HostedServices.Register<BlockNotifier>(() => blockNotifier, "Block Notifier");

		// Initialize index building
		// Initialize index building
		var indexBuilderServiceDir = Path.Combine(DataDir, "IndexBuilderService");
		var segwitTaprootIndexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{RpcClient.Network}.dat");
		var taprootIndexFilePath = Path.Combine(indexBuilderServiceDir, $"TaprootIndex{RpcClient.Network}.dat");

		SegwitTaprootIndexBuilderService = new(IndexType.SegwitTaproot, RpcClient, blockNotifier, segwitTaprootIndexFilePath);
		TaprootIndexBuilderService = new(IndexType.Taproot, RpcClient, blockNotifier, taprootIndexFilePath);
	}

	public string DataDir { get; }

	public IRPCClient RpcClient { get; }

	public P2pNode P2pNode { get; }

	public HostedServices HostedServices { get; }

	public IndexBuilderService SegwitTaprootIndexBuilderService { get; }
	public IndexBuilderService TaprootIndexBuilderService { get; }

	private HttpClient HttpClient { get; }

	public Coordinator? Coordinator { get; private set; }

	public Config Config { get; }

	public CoordinatorRoundConfig? RoundConfig { get; private set; }

	private CoordinatorParameters CoordinatorParameters { get; }

	public CoinJoinIdStore CoinJoinIdStore { get; }
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

		bool coinVerifierEnabled = CoordinatorParameters.RuntimeCoordinatorConfig.IsCoinVerifierEnabled || roundConfig.IsCoinVerifierEnabledForWW1;
		CoinVerifier? coinVerifier = null;
		if (coinVerifierEnabled)
		{
			try
			{
				if (!Uri.TryCreate(CoordinatorParameters.RuntimeCoordinatorConfig.CoinVerifierApiUrl, UriKind.RelativeOrAbsolute, out Uri? url))
				{
					throw new ArgumentException($"Blacklist API URL is invalid in {nameof(WabiSabiConfig)}.");
				}
				if (string.IsNullOrEmpty(CoordinatorParameters.RuntimeCoordinatorConfig.CoinVerifierApiAuthToken))
				{
					throw new ArgumentException($"Blacklist API token was not provided in {nameof(WabiSabiConfig)}.");
				}
				if (CoordinatorParameters.RuntimeCoordinatorConfig.RiskFlags is null)
				{
					throw new ArgumentException($"Risk indicators were not provided in {nameof(WabiSabiConfig)}.");
				}

				HttpClient.BaseAddress = url;

				var coinVerifierApiClient = new CoinVerifierApiClient(CoordinatorParameters.RuntimeCoordinatorConfig.CoinVerifierApiAuthToken, RpcClient.Network, HttpClient);
				var whitelist = await Whitelist.CreateAndLoadFromFileAsync(CoordinatorParameters.WhitelistFilePath, cancel).ConfigureAwait(false);
				coinVerifier = new(CoinJoinIdStore, coinVerifierApiClient, whitelist, CoordinatorParameters.RuntimeCoordinatorConfig);
				Logger.LogInfo("CoinVerifier created successfully.");
			}
			catch (Exception exc)
			{
				Logger.LogCritical($"There was an error when creating {nameof(CoinVerifier)}. Details: '{exc}'");
			}
		}

		Coordinator = new(RpcClient.Network, blockNotifier, Path.Combine(DataDir, "CcjCoordinator"), RpcClient, roundConfig, roundConfig.IsCoinVerifierEnabledForWW1 ? coinVerifier : null);
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

		var coinJoinScriptStore = CoinJoinScriptStore.LoadFromFile(CoordinatorParameters.CoinJoinScriptStoreFilePath);

		WabiSabiCoordinator = new WabiSabiCoordinator(CoordinatorParameters, RpcClient, CoinJoinIdStore, coinJoinScriptStore, CoordinatorParameters.RuntimeCoordinatorConfig.IsCoinVerifierEnabled ? coinVerifier : null);
		HostedServices.Register<WabiSabiCoordinator>(() => WabiSabiCoordinator, "WabiSabi Coordinator");

		HostedServices.Register<RoundBootstrapper>(() => new RoundBootstrapper(TimeSpan.FromMilliseconds(100), Coordinator), "Round Bootstrapper");

		await HostedServices.StartAllAsync(cancel);

		SegwitTaprootIndexBuilderService.Synchronize();
		Logger.LogInfo($"{nameof(SegwitTaprootIndexBuilderService)} is successfully initialized and started synchronization.");
		TaprootIndexBuilderService.Synchronize();
		Logger.LogInfo($"{nameof(TaprootIndexBuilderService)} is successfully initialized and started synchronization.");
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
				HttpClient.Dispose();

				if (Coordinator is { } coordinator)
				{
					coordinator.CoinJoinBroadcasted -= Coordinator_CoinJoinBroadcasted;
					coordinator.Dispose();
					Logger.LogInfo($"{nameof(coordinator)} is disposed.");
				}

				var stoppingTask = Task.Run(async () =>
				{
					await SegwitTaprootIndexBuilderService.StopAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(SegwitTaprootIndexBuilderService)} is stopped.");

					await TaprootIndexBuilderService.StopAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(TaprootIndexBuilderService)} is stopped.");

					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(21));
					await HostedServices.StopAllAsync(cts.Token).ConfigureAwait(false);
					HostedServices.Dispose();

					await P2pNode.DisposeAsync().ConfigureAwait(false);
					Logger.LogInfo($"{nameof(P2pNode)} is disposed.");
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

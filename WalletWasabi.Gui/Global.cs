using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.BitcoinCore.Monitoring;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients;
using WalletWasabi.Crypto;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Gui
{
	public class Global
	{
		public const string GlobalResourceKey = "Wasabi.Ui.Global";
		public const string ConfigResourceKey = "Wasabi.Ui.Config";
		public const string UiConfigResourceKey = "Wasabi.Ui.UiConfig";

		public const string ThemeBackgroundBrushResourceKey = "ThemeBackgroundBrush";
		public const string ApplicationAccentForegroundBrushResourceKey = "ApplicationAccentForegroundBrush";

		public string DataDir { get; }
		public string TorLogsFile { get; }
		public string WalletsDir { get; }
		public string WalletBackupsDir { get; }

		public BitcoinStore BitcoinStore { get; private set; }
		public Config Config { get; private set; }

		public string AddressManagerFilePath { get; private set; }
		public AddressManager AddressManager { get; private set; }

		public NodesGroup Nodes { get; private set; }
		public WasabiSynchronizer Synchronizer { get; private set; }
		public RpcFeeProvider RpcFeeProvider { get; private set; }
		public FeeProviders FeeProviders { get; private set; }
		public CoinJoinClient ChaumianClient { get; private set; }
		public WalletService WalletService { get; private set; }
		public TransactionBroadcaster TransactionBroadcaster { get; set; }
		public Node RegTestMempoolServingNode { get; private set; }
		public UpdateChecker UpdateChecker { get; private set; }
		public TorProcessManager TorManager { get; private set; }
		public CoreNode BitcoinCoreNode { get; private set; }

		public RpcMonitor RpcMonitor { get; private set; }

		public bool KillRequested { get; private set; } = false;

		public UiConfig UiConfig { get; private set; }

		public Network Network => Config.Network;

		public Global()
		{
			DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
			TorLogsFile = Path.Combine(DataDir, "TorLogs.txt");
			WalletsDir = Path.Combine(DataDir, "Wallets");
			WalletBackupsDir = Path.Combine(DataDir, "WalletBackups");

			Directory.CreateDirectory(DataDir);
			Directory.CreateDirectory(WalletsDir);
			Directory.CreateDirectory(WalletBackupsDir);
			RpcMonitor = new RpcMonitor(TimeSpan.FromSeconds(7));
		}

		public void InitializeUiConfig(UiConfig uiConfig)
		{
			UiConfig = Guard.NotNull(nameof(uiConfig), uiConfig);
		}

		private int _isDesperateDequeuing = 0;

		public async Task TryDesperateDequeueAllCoinsAsync()
		{
			// If already desperate dequeuing then return.
			// If not desperate dequeuing then make sure we're doing that.
			if (Interlocked.CompareExchange(ref _isDesperateDequeuing, 1, 0) == 1)
			{
				return;
			}
			try
			{
				await DesperateDequeueAllCoinsAsync();
			}
			catch (NotSupportedException ex)
			{
				Logger.LogWarning(ex.Message);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _isDesperateDequeuing, 0);
			}
		}

		public async Task DesperateDequeueAllCoinsAsync()
		{
			if (WalletService is null || ChaumianClient is null)
			{
				return;
			}

			SmartCoin[] enqueuedCoins = WalletService.Coins.CoinJoinInProcess().ToArray();
			if (enqueuedCoins.Any())
			{
				Logger.LogWarning("Unregistering coins in CoinJoin process.");
				await ChaumianClient.DequeueCoinsFromMixAsync(enqueuedCoins, "Process was signaled to kill.");
			}
		}

		private bool Initialized { get; set; } = false;

		public async Task InitializeNoWalletAsync()
		{
			WalletService = null;
			ChaumianClient = null;
			AddressManager = null;
			TorManager = null;

			#region ConfigInitialization

			Config = new Config(Path.Combine(DataDir, "Config.json"));
			await Config.LoadOrCreateDefaultFileAsync();
			Logger.LogInfo($"{nameof(Config)} is successfully initialized.");

			#endregion ConfigInitialization

			BitcoinStore = new BitcoinStore();
			var bstoreInitTask = BitcoinStore.InitializeAsync(Path.Combine(DataDir, "BitcoinStore"), Network);
			var addressManagerFolderPath = Path.Combine(DataDir, "AddressManager");

			AddressManagerFilePath = Path.Combine(addressManagerFolderPath, $"AddressManager{Network}.dat");
			var addrManTask = InitializeAddressManagerBehaviorAsync();

			var blocksFolderPath = Path.Combine(DataDir, $"Blocks{Network}");
			var connectionParameters = new NodeConnectionParameters { UserAgent = "/Satoshi:0.18.1/" };

			if (Config.UseTor)
			{
				Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, () => Config.GetCurrentBackendUri(), Config.TorSocks5EndPoint);
			}
			else
			{
				Synchronizer = new WasabiSynchronizer(Network, BitcoinStore, Config.GetFallbackBackendUri(), null);
			}

			UpdateChecker = new UpdateChecker(TimeSpan.FromMinutes(7), Synchronizer.WasabiClient);

			#region ProcessKillSubscription

			AppDomain.CurrentDomain.ProcessExit += async (s, e) => await TryDesperateDequeueAllCoinsAsync();
			Console.CancelKeyPress += async (s, e) =>
			{
				e.Cancel = true;
				Logger.LogWarning("Process was signaled for killing.");

				KillRequested = true;
				await TryDesperateDequeueAllCoinsAsync();
				Dispatcher.UIThread.PostLogException(() =>
				{
					var window = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow;
					window?.Close();
				});
				await DisposeAsync();

				Logger.LogSoftwareStopped("Wasabi");
			};

			#endregion ProcessKillSubscription

			#region TorProcessInitialization

			if (Config.UseTor)
			{
				TorManager = new TorProcessManager(Config.TorSocks5EndPoint, TorLogsFile);
			}
			else
			{
				TorManager = TorProcessManager.Mock();
			}
			TorManager.Start(false, DataDir);

			var fallbackRequestTestUri = new Uri(Config.GetFallbackBackendUri(), "/api/software/versions");
			TorManager.StartMonitor(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7), DataDir, fallbackRequestTestUri);

			Logger.LogInfo($"{nameof(TorProcessManager)} is initialized.");

			#endregion TorProcessInitialization

			#region BitcoinStoreInitialization

			await bstoreInitTask;

			#endregion BitcoinStoreInitialization

			#region BitcoinCoreInitialization

			var feeProviderList = new List<IFeeProvider>();
			try
			{
				if (Config.StartLocalBitcoinCoreOnStartup)
				{
					BitcoinCoreNode = await CoreNode
						.CreateAsync(
							new CoreNodeParams(
								Network,
								BitcoinStore.MempoolService,
								Config.LocalBitcoinCoreDataDir,
								tryRestart: false,
								tryDeleteDataDir: false,
								EndPointStrategy.Custom(Config.GetBitcoinP2pEndPoint()),
								EndPointStrategy.Default(Network, EndPointType.Rpc),
								txIndex: null,
								prune: null,
								userAgent: $"/WasabiClient:{Constants.ClientVersion.ToString()}/"),
							CancellationToken.None)
						.ConfigureAwait(false);

					RpcMonitor.RpcClient = BitcoinCoreNode.RpcClient;
					RpcMonitor.Start();

					RpcFeeProvider = new RpcFeeProvider(TimeSpan.FromMinutes(1), BitcoinCoreNode.RpcClient);
					RpcFeeProvider.Start();
					feeProviderList.Add(RpcFeeProvider);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}

			feeProviderList.Add(Synchronizer);
			FeeProviders = new FeeProviders(feeProviderList);

			#endregion BitcoinCoreInitialization

			#region MempoolInitialization

			connectionParameters.TemplateBehaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());

			#endregion MempoolInitialization

			#region AddressManagerInitialization

			AddressManagerBehavior addressManagerBehavior = await addrManTask;
			connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);

			#endregion AddressManagerInitialization

			#region P2PInitialization

			if (Network == Network.RegTest)
			{
				Nodes = new NodesGroup(Network, requirements: Constants.NodeRequirements);
				try
				{
					EndPoint bitcoinCoreEndpoint = Config.GetBitcoinP2pEndPoint();

					Node node = await Node.ConnectAsync(Network.RegTest, bitcoinCoreEndpoint).ConfigureAwait(false);

					Nodes.ConnectedNodes.Add(node);

					RegTestMempoolServingNode = await Node.ConnectAsync(Network.RegTest, bitcoinCoreEndpoint).ConfigureAwait(false);

					RegTestMempoolServingNode.Behaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());
				}
				catch (SocketException ex)
				{
					Logger.LogError(ex);
				}
			}
			else
			{
				if (Config.UseTor is true)
				{
					// onlyForOnionHosts: false - Connect to clearnet IPs through Tor, too.
					connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(Config.TorSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
					// allowOnlyTorEndpoints: true - Connect only to onions and do not connect to clearnet IPs at all.
					// This of course makes the first setting unnecessary, but it's better if that's around, in case someone wants to tinker here.
					connectionParameters.EndpointConnector = new DefaultEndpointConnector(allowOnlyTorEndpoints: Network == Network.Main);

					await AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager);
				}
				Nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);

				RegTestMempoolServingNode = null;
			}

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

			var regTestMempoolServingNode = RegTestMempoolServingNode;
			if (RegTestMempoolServingNode is { })
			{
				regTestMempoolServingNode.VersionHandshake();
				Logger.LogInfo("Start connecting to mempool serving regtest node...");
			}

			#endregion P2PInitialization

			#region SynchronizerInitialization

			var requestInterval = TimeSpan.FromSeconds(30);
			if (Network == Network.RegTest)
			{
				requestInterval = TimeSpan.FromSeconds(5);
			}

			int maxFiltSyncCount = Network == Network.Main ? 1000 : 10000; // On testnet, filters are empty, so it's faster to query them together

			Synchronizer.Start(requestInterval, TimeSpan.FromMinutes(5), maxFiltSyncCount);
			Logger.LogInfo("Start synchronizing filters...");

			#endregion SynchronizerInitialization

			TransactionBroadcaster = new TransactionBroadcaster(Network, BitcoinStore, Synchronizer, Nodes, BitcoinCoreNode?.RpcClient);

			Initialized = true;
		}

		private async Task<AddressManagerBehavior> InitializeAddressManagerBehaviorAsync()
		{
			var needsToDiscoverPeers = true;
			if (Network == Network.RegTest)
			{
				AddressManager = new AddressManager();
				Logger.LogInfo($"Fake {nameof(AddressManager)} is initialized on the {Network.RegTest}.");
			}
			else
			{
				try
				{
					AddressManager = await NBitcoinHelpers.LoadAddressManagerFromPeerFileAsync(AddressManagerFilePath);

					// Most of the times we do not need to discover new peers. Instead, we can connect to
					// some of those that we already discovered in the past. In this case we assume that
					// discovering new peers could be necessary if our address manager has less
					// than 500 addresses. 500 addresses could be okay because previously we tried with
					// 200 and only one user reported he/she was not able to connect (there could be many others,
					// of course).
					// On the other side, increasing this number forces users that do not need to discover more peers
					// to spend resources (CPU/bandwith) to discover new peers.
					needsToDiscoverPeers = Config.UseTor is true || AddressManager.Count < 500;
					Logger.LogInfo($"Loaded {nameof(AddressManager)} from `{AddressManagerFilePath}`.");
				}
				catch (DirectoryNotFoundException ex)
				{
					Logger.LogInfo($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
				}
				catch (FileNotFoundException ex)
				{
					Logger.LogInfo($"{nameof(AddressManager)} did not exist at `{AddressManagerFilePath}`. Initializing new one.");
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
				}
				catch (OverflowException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/712
					Logger.LogInfo($"{nameof(AddressManager)} has thrown `{nameof(OverflowException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo($"{nameof(AddressManager)} autocorrection is successful.");
				}
				catch (FormatException ex)
				{
					// https://github.com/zkSNACKs/WalletWasabi/issues/880
					Logger.LogInfo($"{nameof(AddressManager)} has thrown `{nameof(FormatException)}`. Attempting to autocorrect.");
					File.Delete(AddressManagerFilePath);
					Logger.LogTrace(ex);
					AddressManager = new AddressManager();
					Logger.LogInfo($"{nameof(AddressManager)} autocorrection is successful.");
				}
			}

			var addressManagerBehavior = new AddressManagerBehavior(AddressManager)
			{
				Mode = needsToDiscoverPeers ? AddressManagerBehaviorMode.Discover : AddressManagerBehaviorMode.None
			};
			return addressManagerBehavior;
		}

		private async Task AddKnownBitcoinFullNodeAsHiddenServiceAsync(AddressManager addressManager)
		{
			if (Network == Network.RegTest)
			{
				return;
			}

			// curl -s https://bitnodes.21.co/api/v1/snapshots/latest/ | egrep -o '[a-z0-9]{16}\.onion:?[0-9]*' | sort -ru
			// Then filtered to include only /Satoshi:0.17.x
			var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

			var onions = await File.ReadAllLinesAsync(Path.Combine(fullBaseDirectory, "OnionSeeds", $"{Network}OnionSeeds.txt"));

			onions.Shuffle();
			foreach (var onion in onions.Take(60))
			{
				if (Utils.TryParseEndpoint(onion, Network.DefaultPort, out var endpoint))
				{
					await addressManager.AddAsync(endpoint);
				}
			}
		}

		private CancellationTokenSource _cancelWalletServiceInitialization = null;

		public async Task InitializeWalletServiceAsync(KeyManager keyManager)
		{
			using (_cancelWalletServiceInitialization = new CancellationTokenSource())
			{
				var token = _cancelWalletServiceInitialization.Token;
				while (!Initialized)
				{
					await Task.Delay(100, token);
				}

				if (Config.UseTor)
				{
					ChaumianClient = new CoinJoinClient(Synchronizer, Network, keyManager, () => Config.GetCurrentBackendUri(), Config.TorSocks5EndPoint);
				}
				else
				{
					ChaumianClient = new CoinJoinClient(Synchronizer, Network, keyManager, Config.GetFallbackBackendUri(), null);
				}

				try
				{
					keyManager.CorrectBlockHeights(BitcoinStore.HashChain); // Block heights are wrong sometimes. It's a hack. We have to retroactively fix existing wallets, but also we have to figure out where we ruin the block heights.
				}
				catch (Exception ex) // Whatever this is not critical, but let's log it.
				{
					Logger.LogWarning(ex);
				}

				WalletService = new WalletService(BitcoinStore, keyManager, Synchronizer, ChaumianClient, Nodes, DataDir, Config.ServiceConfiguration, FeeProviders, BitcoinCoreNode);

				ChaumianClient.Start();
				Logger.LogInfo("Start Chaumian CoinJoin service...");

				Logger.LogInfo($"Starting {nameof(WalletService)}...");
				await WalletService.InitializeAsync(token);
				Logger.LogInfo($"{nameof(WalletService)} started.");

				token.ThrowIfCancellationRequested();
				WalletService.TransactionProcessor.CoinReceived += CoinReceived;

				TransactionBroadcaster.AddWalletService(WalletService);
			}
			_cancelWalletServiceInitialization = null; // Must make it null explicitly, because dispose won't make it null.
		}

		public string GetWalletFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletsDir, walletName + ".json");
		}

		public string GetWalletBackupFullPath(string walletName)
		{
			walletName = walletName.TrimEnd(".json", StringComparison.OrdinalIgnoreCase);
			return Path.Combine(WalletBackupsDir, walletName + ".json");
		}

		public KeyManager LoadKeyManager(string walletFullPath, string walletBackupFullPath)
		{
			try
			{
				return LoadKeyManager(walletFullPath);
			}
			catch (Exception ex)
			{
				if (!File.Exists(walletBackupFullPath))
				{
					throw;
				}

				Logger.LogWarning($"Wallet got corrupted.\n" +
					$"Wallet Filepath: {walletFullPath}\n" +
					$"Trying to recover it from backup.\n" +
					$"Backup path: {walletBackupFullPath}\n" +
					$"Exception: {ex}");
				if (File.Exists(walletFullPath))
				{
					string corruptedWalletBackupPath = Path.Combine(WalletBackupsDir, $"{Path.GetFileName(walletFullPath)}_CorruptedBackup");
					if (File.Exists(corruptedWalletBackupPath))
					{
						File.Delete(corruptedWalletBackupPath);
						Logger.LogInfo($"Deleted previous corrupted wallet file backup from `{corruptedWalletBackupPath}`.");
					}
					File.Move(walletFullPath, corruptedWalletBackupPath);
					Logger.LogInfo($"Backed up corrupted wallet file to `{corruptedWalletBackupPath}`.");
				}
				File.Copy(walletBackupFullPath, walletFullPath);

				return LoadKeyManager(walletFullPath);
			}
		}

		public KeyManager LoadKeyManager(string walletFullPath)
		{
			KeyManager keyManager;

			// Set the LastAccessTime.
			new FileInfo(walletFullPath)
			{
				LastAccessTime = DateTime.Now
			};

			keyManager = KeyManager.FromFile(walletFullPath);
			Logger.LogInfo($"Wallet loaded: {Path.GetFileNameWithoutExtension(keyManager.FilePath)}.");
			return keyManager;
		}

		private void CoinReceived(object sender, SmartCoin coin)
		{
			try
			{
				if (coin.HdPubKey.IsInternal)
				{
					return;
				}

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || UiConfig?.ShieldedScreenMode is true)
				{
					return;
				}

				string amountString = coin.Amount.ToString(false, true);
				using var process = Process.Start(new ProcessStartInfo
				{
					FileName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osascript" : "notify-send",
					Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
						? $"-e \"display notification \\\"Received {amountString} BTC\\\" with title \\\"Wasabi\\\"\""
						: $"--expire-time=3000 \"Wasabi\" \"Received {amountString} BTC\"",
					CreateNoWindow = true
				});
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
		}

		public async Task DisposeInWalletDependentServicesAsync()
		{
			var walletService = WalletService;
			if (walletService is { })
			{
				walletService.TransactionProcessor.CoinReceived -= CoinReceived;
			}

			try
			{
				_cancelWalletServiceInitialization?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				Logger.LogWarning($"{nameof(_cancelWalletServiceInitialization)} is disposed. This can occur due to an error while processing the wallet.");
			}
			_cancelWalletServiceInitialization = null;

			walletService = WalletService;
			if (walletService is { })
			{
				var keyManager = walletService.KeyManager;
				if (keyManager is { }) // This should not ever happen.
				{
					string backupWalletFilePath = Path.Combine(WalletBackupsDir, Path.GetFileName(keyManager.FilePath));
					keyManager.ToFile(backupWalletFilePath);
					Logger.LogInfo($"{nameof(walletService.KeyManager)} backup saved to `{backupWalletFilePath}`.");
				}
				walletService?.Dispose();
				WalletService = null;
				Logger.LogInfo($"{nameof(WalletService)} is stopped.");
			}

			var chaumianClient = ChaumianClient;
			if (chaumianClient is { })
			{
				await chaumianClient.StopAsync();
				ChaumianClient = null;
				Logger.LogInfo($"{nameof(ChaumianClient)} is stopped.");
			}
		}

		/// <summary>
		/// 0: nobody called
		/// 1: somebody called
		/// 2: call finished
		/// </summary>
		private long _dispose = 0; // To detect redundant calls

		public async Task DisposeAsync()
		{
			var compareRes = Interlocked.CompareExchange(ref _dispose, 1, 0);
			if (compareRes == 1)
			{
				while (Interlocked.Read(ref _dispose) != 2)
				{
					await Task.Delay(50);
				}
				return;
			}
			else if (compareRes == 2)
			{
				return;
			}

			try
			{
				await DisposeInWalletDependentServicesAsync();

				var updateChecker = UpdateChecker;
				if (updateChecker is { })
				{
					await updateChecker.StopAsync();
					Logger.LogInfo($"{nameof(UpdateChecker)} is stopped.");
				}

				var feeProviders = FeeProviders;
				if (feeProviders is { })
				{
					feeProviders.Dispose();
					Logger.LogInfo($"Disposed {nameof(FeeProviders)}.");
				}

				var synchronizer = Synchronizer;
				if (synchronizer is { })
				{
					await synchronizer.StopAsync();
					Logger.LogInfo($"{nameof(Synchronizer)} is stopped.");
				}

				var rpcFeeProvider = RpcFeeProvider;
				if (rpcFeeProvider is { })
				{
					await rpcFeeProvider.StopAsync();
					Logger.LogInfo("Stopped synching fees through RPC.");
				}

				var addressManagerFilePath = AddressManagerFilePath;
				if (addressManagerFilePath is { })
				{
					IoHelpers.EnsureContainingDirectoryExists(addressManagerFilePath);
					var addressManager = AddressManager;
					if (addressManager is { })
					{
						addressManager.SavePeerFile(AddressManagerFilePath, Config.Network);
						Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.");
					}
				}

				var nodes = Nodes;
				if (nodes is { })
				{
					nodes.Disconnect();
					while (nodes.ConnectedNodes.Any(x => x.IsConnected))
					{
						await Task.Delay(50);
					}
					nodes.Dispose();
					Logger.LogInfo($"{nameof(Nodes)} are disposed.");
				}

				var regTestMempoolServingNode = RegTestMempoolServingNode;
				if (regTestMempoolServingNode is { })
				{
					regTestMempoolServingNode.Disconnect();
					Logger.LogInfo($"{nameof(RegTestMempoolServingNode)} is disposed.");
				}

				var rpcMonitor = RpcMonitor;
				if (rpcMonitor is { })
				{
					await rpcMonitor.StopAsync();
					Logger.LogInfo("Stopped monitoring RPC.");
				}

				var bitcoinCoreNode = BitcoinCoreNode;
				if (bitcoinCoreNode is { })
				{
					await bitcoinCoreNode.DisposeAsync().ConfigureAwait(false);

					if (Config.StopLocalBitcoinCoreOnShutdown)
					{
						await bitcoinCoreNode.TryStopAsync().ConfigureAwait(false);
					}
				}

				var torManager = TorManager;
				if (torManager is { })
				{
					await torManager.StopAsync();
					Logger.LogInfo($"{nameof(TorManager)} is stopped.");
				}

				if (AsyncMutex.IsAny)
				{
					try
					{
						await AsyncMutex.WaitForAllMutexToCloseAsync();
						Logger.LogInfo($"{nameof(AsyncMutex)}(es) are stopped.");
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error during stopping {nameof(AsyncMutex)}: {ex}");
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex);
			}
			finally
			{
				Interlocked.Exchange(ref _dispose, 2);
			}
		}

		public string GetNextWalletName()
		{
			for (int i = 0; i < int.MaxValue; i++)
			{
				if (!File.Exists(Path.Combine(WalletsDir, $"Wallet{i}.json")))
				{
					return $"Wallet{i}";
				}
			}

			throw new NotSupportedException("This is impossible.");
		}

		public string GetNextHardwareWalletName(HwiEnumerateEntry hwi = null, string customPrefix = null)
		{
			var prefix = customPrefix is null
				? hwi is null
					? "HardwareWallet"
					: hwi.Model.ToString()
				: customPrefix;

			for (int i = 0; i < int.MaxValue; i++)
			{
				var name = $"{prefix}{i}";
				if (!File.Exists(Path.Combine(WalletsDir, $"{name}.json")))
				{
					return name;
				}
			}

			throw new NotSupportedException("This is impossible.");
		}
	}
}

using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using NBitcoin.Protocol.Connectors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.P2p
{
	public class P2pClient : BackgroundService
	{
		public P2pClient(string dataDir, Network network, bool useTor, EndPoint bitcoinCoreEndpoint, EndPoint torSocks5EndPoint, BitcoinStore bitcoinStore)
		{
			using (BenchmarkLogger.Measure())
			{
				DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
				Network = Guard.NotNull(nameof(network), network);
				UseTor = useTor;
				BitcoinCoreEndpoint = Guard.NotNull(nameof(bitcoinCoreEndpoint), bitcoinCoreEndpoint);
				TorSocks5EndPoint = Guard.NotNull(nameof(torSocks5EndPoint), torSocks5EndPoint);
				BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);

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
						AddressManager = NBitcoinHelpers.LoadAddressManagerFromPeerFile(AddressManagerFilePath);

						// Most of the times we do not need to discover new peers. Instead, we can connect to
						// some of those that we already discovered in the past. In this case we assume that
						// discovering new peers could be necessary if our address manager has less
						// than 500 addresses. 500 addresses could be okay because previously we tried with
						// 200 and only one user reported he/she was not able to connect (there could be many others,
						// of course).
						// On the other side, increasing this number forces users that do not need to discover more peers
						// to spend resources (CPU/bandwidth) to discover new peers.
						needsToDiscoverPeers = UseTor || AddressManager.Count < 500;
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

				ConnectionParameters = new NodeConnectionParameters { UserAgent = $"/Satoshi:{Constants.BitcoinCoreVersion}/" };
				ConnectionParameters.TemplateBehaviors.Add(addressManagerBehavior);
				ConnectionParameters.TemplateBehaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());
			}
		}

		public string DataDir { get; }
		public Network Network { get; }
		public bool UseTor { get; }
		public AddressManager AddressManager { get; }
		public string AddressManagerFolderPath => Path.Combine(DataDir, "AddressManager");
		public string AddressManagerFilePath => Path.Combine(AddressManagerFolderPath, $"AddressManager{Network}.dat");

		public EndPoint BitcoinCoreEndpoint { get; }
		public NodesGroup Nodes { get; private set; }
		public Node RegTestMempoolServingNode { get; private set; }
		public NodeConnectionParameters ConnectionParameters { get; }
		public EndPoint TorSocks5EndPoint { get; }
		public BitcoinStore BitcoinStore { get; }

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			if (Network == Network.RegTest)
			{
				Nodes = new NodesGroup(Network, requirements: Constants.NodeRequirements);
				try
				{
					Node node = await Node.ConnectAsync(Network.RegTest, BitcoinCoreEndpoint).ConfigureAwait(false);
					cancellationToken.ThrowIfCancellationRequested();

					Nodes.ConnectedNodes.Add(node);

					RegTestMempoolServingNode = await Node.ConnectAsync(Network.RegTest, BitcoinCoreEndpoint).ConfigureAwait(false);
					cancellationToken.ThrowIfCancellationRequested();

					RegTestMempoolServingNode.Behaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());
				}
				catch (SocketException ex)
				{
					Logger.LogError(ex);
				}
			}
			else
			{
				if (UseTor)
				{
					// onlyForOnionHosts: false - Connect to clearnet IPs through Tor, too.
					ConnectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(TorSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
					// allowOnlyTorEndpoints: true - Connect only to onions and do not connect to clearnet IPs at all.
					// This of course makes the first setting unnecessary, but it's better if that's around, in case someone wants to tinker here.
					ConnectionParameters.EndpointConnector = new DefaultEndpointConnector(allowOnlyTorEndpoints: Network == Network.Main);

					if (Network == Network.RegTest)
					{
						return;
					}

					// curl -s https://bitnodes.21.co/api/v1/snapshots/latest/ | egrep -o '[a-z0-9]{16}\.onion:?[0-9]*' | sort -ru
					// Then filtered to include only /Satoshi:0.17.x
					var fullBaseDirectory = EnvironmentHelpers.GetFullBaseDirectory();

					var onions = await File.ReadAllLinesAsync(Path.Combine(fullBaseDirectory, "OnionSeeds", $"{Network}OnionSeeds.txt"));
					cancellationToken.ThrowIfCancellationRequested();

					onions.Shuffle();
					foreach (var onion in onions.Take(60))
					{
						if (EndPointParser.TryParse(onion, Network.DefaultPort, out var endpoint))
						{
							await AddressManager.AddAsync(endpoint);
							cancellationToken.ThrowIfCancellationRequested();
						}
					}
				}
				Nodes = new NodesGroup(Network, ConnectionParameters, requirements: Constants.NodeRequirements);

				RegTestMempoolServingNode = null;
			}

			Nodes.Connect();
			Logger.LogInfo("Start connecting to nodes...");

			var regTestMempoolServingNode = RegTestMempoolServingNode;
			if (regTestMempoolServingNode is { })
			{
				regTestMempoolServingNode.VersionHandshake();
				Logger.LogInfo("Start connecting to mempool serving regtest node...");
			}

			await base.StartAsync(cancellationToken).ConfigureAwait(false);
		}

		protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			await base.StopAsync(cancellationToken).ConfigureAwait(false);

			var addressManagerFilePath = AddressManagerFilePath;
			if (addressManagerFilePath is { })
			{
				IoHelpers.EnsureContainingDirectoryExists(addressManagerFilePath);
				var addressManager = AddressManager;
				if (addressManager is { })
				{
					addressManager.SavePeerFile(AddressManagerFilePath, Network);
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
		}
	}
}

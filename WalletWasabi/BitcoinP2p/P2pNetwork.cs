using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Stores;

namespace WalletWasabi.BitcoinP2p;

public class P2pNetwork : BackgroundService
{
	/// <summary>Maximum number of nodes to establish connection to.</summary>
	private const int MaximumNodeConnections = 12;

	public P2pNetwork(Network network, EndPoint fullNodeP2pEndPoint, EndPoint? torSocks5EndPoint, string workDir, BitcoinStore bitcoinStore)
	{
		Network = network;
		FullNodeP2PEndPoint = fullNodeP2pEndPoint;
		BitcoinStore = bitcoinStore;
		AddressManagerFilePath = Path.Combine(workDir, $"AddressManager{Network}.dat");

		if (Network == Network.RegTest)
		{
			AddressManager = new AddressManager();
			Logger.LogInfo($"Fake {nameof(AddressManager)} is initialized on the {Network.RegTest}.");

			Nodes = new NodesGroup(Network, requirements: Constants.NodeRequirements);
		}
		else
		{
			var needsToDiscoverPeers = true;

			try
			{
				AddressManager = AddressManager.LoadPeerFile(AddressManagerFilePath);

				// Most of the times we do not need to discover new peers. Instead, we can connect to
				// some of those that we already discovered in the past. In this case we assume that
				// discovering new peers could be necessary if our address manager has less
				// than 500 addresses. 500 addresses could be okay because previously we tried with
				// 200 and only one user reported he/she was not able to connect (there could be many others,
				// of course).
				// On the other side, increasing this number forces users that do not need to discover more peers
				// to spend resources (CPU/bandwidth) to discover new peers.
				needsToDiscoverPeers = torSocks5EndPoint is not null || AddressManager.Count < 500;
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
			catch (Exception ex) when (ex is OverflowException || ex is FormatException || ex is ArgumentException || ex is EndOfStreamException)
			{
				// https://github.com/WalletWasabi/WalletWasabi/issues/712
				// https://github.com/WalletWasabi/WalletWasabi/issues/880
				// https://www.reddit.com/r/WasabiWallet/comments/qt0mgz/crashing_on_open/
				// https://github.com/WalletWasabi/WalletWasabi/issues/5255
				Logger.LogInfo($"{nameof(AddressManager)} has thrown `{ex.GetType().Name}`. Attempting to autocorrect.");
				File.Delete(AddressManagerFilePath);
				Logger.LogTrace(ex);
				AddressManager = new AddressManager();
				Logger.LogInfo($"{nameof(AddressManager)} autocorrection is successful.");
			}

			var addressManagerBehavior = new AddressManagerBehavior(AddressManager)
			{
				Mode = needsToDiscoverPeers ? AddressManagerBehaviorMode.Discover : AddressManagerBehaviorMode.None
			};

			var userAgent = Constants.UserAgents.RandomElement(SecureRandom.Instance);
			var connectionParameters = new NodeConnectionParameters { UserAgent = userAgent };

			connectionParameters.TemplateBehaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());
			connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);
			connectionParameters.EndpointConnector = new BestEffortEndpointConnector(MaximumNodeConnections / 2);

			if (torSocks5EndPoint is not null)
			{
				connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(torSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
			}
			var nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Added += ConnectedNodes_OnAddedOrRemoved;
			nodes.ConnectedNodes.Removed += ConnectedNodes_OnAddedOrRemoved;
			nodes.MaximumNodeConnection = MaximumNodeConnections;

			Nodes = nodes;
		}
	}

	private Network Network { get; }
	private EndPoint FullNodeP2PEndPoint { get; }
	private BitcoinStore BitcoinStore { get; }
	public NodesGroup Nodes { get; }
	private Node? RegTestMempoolServingNode { get; set; }
	private string AddressManagerFilePath { get; }
	private AddressManager AddressManager { get; }

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (Network == Network.RegTest)
		{
			try
			{
				Node node = await Node.ConnectAsync(Network.RegTest, FullNodeP2PEndPoint).ConfigureAwait(false);

				Nodes.ConnectedNodes.Add(node);

				RegTestMempoolServingNode = await Node.ConnectAsync(Network.RegTest, FullNodeP2PEndPoint).ConfigureAwait(false);

				RegTestMempoolServingNode.Behaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());
			}
			catch (SocketException ex)
			{
				Logger.LogError(ex);
			}
		}

		Nodes.Connect();

		var regTestMempoolServingNode = RegTestMempoolServingNode;
		if (regTestMempoolServingNode is not null)
		{
			regTestMempoolServingNode.VersionHandshake(stoppingToken);
			Logger.LogInfo("Start connecting to mempool serving regtest node...");
		}
	}

	private void ConnectedNodes_OnAddedOrRemoved(object? sender, NodeEventArgs e)
	{
		var nodes = Nodes;
		if (nodes is not null && sender is NodesCollection nodesCollection && nodes.NodeConnectionParameters.EndpointConnector is BestEffortEndpointConnector bestEffortEndPointConnector)
		{
			bestEffortEndPointConnector.UpdateConnectedNodesCounter(nodesCollection.Count);
		}
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		IoHelpers.EnsureContainingDirectoryExists(AddressManagerFilePath);

		AddressManager.SavePeerFile(AddressManagerFilePath, Network);
		Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.");

		cancellationToken.ThrowIfCancellationRequested();

		Nodes.Disconnect();
		while (Nodes.ConnectedNodes.Any(x => x.IsConnected))
		{
			await Task.Delay(50, cancellationToken).ConfigureAwait(false);
		}

		cancellationToken.ThrowIfCancellationRequested();

		if (RegTestMempoolServingNode is { } regTestMempoolServingNode)
		{
			regTestMempoolServingNode.Disconnect();
			Logger.LogInfo($"{nameof(RegTestMempoolServingNode)} is disposed.");
		}

		await base.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		if (Network != Network.RegTest)
		{
			Nodes.ConnectedNodes.Added -= ConnectedNodes_OnAddedOrRemoved;
			Nodes.ConnectedNodes.Removed -= ConnectedNodes_OnAddedOrRemoved;
		}

		Nodes.Dispose();
		base.Dispose();
	}
}

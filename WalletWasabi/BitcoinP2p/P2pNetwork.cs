using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.BitcoinP2p;

public static class P2pNetwork
{
	/// <summary>Maximum number of nodes to establish connection to.</summary>
	private const int MaximumNodeConnections = 12;

	public static NodesGroup CreateNodesGroupForTestNet(P2pBehavior p2PBehavior)
	{
		var nodesGroup = new NodesGroup(Network.RegTest);
		try
		{
			var localNodelEndpoint = new IPEndPoint(IPAddress.Loopback, Network.RegTest.DefaultPort);
			var node = Node.Connect(Network.RegTest, localNodelEndpoint);
			node.Behaviors.Add(p2PBehavior);
			node.VersionHandshake(CancellationToken.None);
			Logger.LogInfo("Start connecting to mempool serving regtest node...");

			nodesGroup.ConnectedNodes.Add(node);
			return nodesGroup;
		}
		catch (SocketException ex)
		{
			Logger.LogError(ex);
			throw;
		}
	}

	public static NodesGroup CreateNodesGroup(Network network, EndPoint? torSocks5EndPoint, string workDir, P2pBehavior? p2PBehavior = null)
	{
		var addressManagerFilePath = Path.Combine(workDir, $"AddressManager{network}.dat");
		var connectionParameters = new NodeConnectionParameters();

		var addressManager = LoadOrCreateAddressManager(addressManagerFilePath);

		var useTor = torSocks5EndPoint is not null;
		var needsToDiscoverPeers = useTor || addressManager.Count < 500;
		var addressManagerBehavior = new AddressManagerBehavior(addressManager)
		{
			Mode = needsToDiscoverPeers ? AddressManagerBehaviorMode.Discover : AddressManagerBehaviorMode.None
		};

		var userAgent = Constants.UserAgents.RandomElement(SecureRandom.Instance);
		connectionParameters.UserAgent = userAgent;
		connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);
		connectionParameters.EndpointConnector = new BestEffortEndpointConnector(MaximumNodeConnections / 2);
		if (p2PBehavior is not null)
		{
			connectionParameters.TemplateBehaviors.Add(p2PBehavior);
		}

		if (useTor)
		{
			connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(torSocks5EndPoint,
				onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
		}

		return new NodesGroup(network, connectionParameters, Constants.NodeRequirements);
	}

	public static Process<Unit> Create(NodesGroup nodesGroup, EventBus eventBus) =>
		async (_, cancellationToken) =>
		{
			nodesGroup.ConnectedNodes.Added += ConnectedNodes_OnAddedOrRemoved;
			nodesGroup.ConnectedNodes.Removed += ConnectedNodes_OnAddedOrRemoved;
			nodesGroup.MaximumNodeConnection = MaximumNodeConnections;
			nodesGroup.Connect();

			await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

			nodesGroup.ConnectedNodes.Added -= ConnectedNodes_OnAddedOrRemoved;
			nodesGroup.ConnectedNodes.Removed -= ConnectedNodes_OnAddedOrRemoved;

			nodesGroup.Disconnect();
			return;

			void ConnectedNodes_OnAddedOrRemoved(object? sender, NodeEventArgs e)
			{
				if (sender is NodesCollection nodesCollection)
				{
					if (nodesGroup.NodeConnectionParameters.EndpointConnector is BestEffortEndpointConnector
					    bestEffortEndPointConnector)
					{
						bestEffortEndPointConnector.UpdateConnectedNodesCounter(nodesCollection.Count);
					}

					eventBus.Publish(new BitcoinPeersChanged(e.Added, nodesCollection.Count));
				}
			}
		};

	private static AddressManager LoadOrCreateAddressManager(string addressManagerFilePath)
	{
		try
		{
			var addressManager = AddressManager.LoadPeerFile(addressManagerFilePath);

			// Most of the times we do not need to discover new peers. Instead, we can connect to
			// some of those that we already discovered in the past. In this case we assume that
			// discovering new peers could be necessary if our address manager has less
			// than 500 addresses. 500 addresses could be okay because previously we tried with
			// 200 and only one user reported he/she was not able to connect (there could be many others,
			// of course).
			// On the other side, increasing this number forces users that do not need to discover more peers
			// to spend resources (CPU/bandwidth) to discover new peers.
			Logger.LogInfo($"Loaded {nameof(AddressManager)} from `{addressManagerFilePath}`.");
			return addressManager;
		}
		catch (IOException ex) when (ex is DirectoryNotFoundException or FileNotFoundException)
		{
			Logger.LogInfo($"{nameof(AddressManager)} did not exist at `{addressManagerFilePath}`. Initializing new one.");
			Logger.LogTrace(ex);
			return new AddressManager();
		}
		catch (Exception ex) when (ex is OverflowException or FormatException or ArgumentException or EndOfStreamException)
		{
			// https://github.com/WalletWasabi/WalletWasabi/issues/712
			// https://github.com/WalletWasabi/WalletWasabi/issues/880
			// https://www.reddit.com/r/WasabiWallet/comments/qt0mgz/crashing_on_open/
			// https://github.com/WalletWasabi/WalletWasabi/issues/5255
			Logger.LogInfo($"{nameof(AddressManager)} has thrown `{ex.GetType().Name}`. Attempting to autocorrect.");
			File.Delete(addressManagerFilePath);
			Logger.LogTrace(ex);
			var addressManager = new AddressManager();
			Logger.LogInfo($"{nameof(AddressManager)} autocorrection is successful.");
			return addressManager;
		}
	}

}

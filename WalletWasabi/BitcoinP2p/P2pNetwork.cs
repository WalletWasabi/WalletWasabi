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
		_network = network;
		_fullNodeP2PEndPoint = fullNodeP2pEndPoint;
		_bitcoinStore = bitcoinStore;
		_addressManagerFilePath = Path.Combine(workDir, $"_addressManager{_network}.dat");

		if (_network == Network.RegTest)
		{
			_addressManager = new AddressManager();
			Logger.LogInfo($"Fake {nameof(AddressManager)} is initialized on the {Network.RegTest}.");

			Nodes = new NodesGroup(_network, requirements: Constants.NodeRequirements);
		}
		else
		{
			var needsToDiscoverPeers = true;

			try
			{
				_addressManager = AddressManager.LoadPeerFile(_addressManagerFilePath);

				// Most of the times we do not need to discover new peers. Instead, we can connect to
				// some of those that we already discovered in the past. In this case we assume that
				// discovering new peers could be necessary if our address manager has less
				// than 500 addresses. 500 addresses could be okay because previously we tried with
				// 200 and only one user reported he/she was not able to connect (there could be many others,
				// of course).
				// On the other side, increasing this number forces users that do not need to discover more peers
				// to spend resources (CPU/bandwidth) to discover new peers.
				needsToDiscoverPeers = torSocks5EndPoint is not null || _addressManager.Count < 500;
				Logger.LogInfo($"Loaded {nameof(AddressManager)} from `{_addressManagerFilePath}`.");
			}
			catch (DirectoryNotFoundException ex)
			{
				Logger.LogInfo($"{nameof(AddressManager)} did not exist at `{_addressManagerFilePath}`. Initializing new one.");
				Logger.LogTrace(ex);
				_addressManager = new AddressManager();
			}
			catch (FileNotFoundException ex)
			{
				Logger.LogInfo($"{nameof(AddressManager)} did not exist at `{_addressManagerFilePath}`. Initializing new one.");
				Logger.LogTrace(ex);
				_addressManager = new AddressManager();
			}
			catch (Exception ex) when (ex is OverflowException || ex is FormatException || ex is ArgumentException || ex is EndOfStreamException)
			{
				// https://github.com/WalletWasabi/WalletWasabi/issues/712
				// https://github.com/WalletWasabi/WalletWasabi/issues/880
				// https://www.reddit.com/r/WasabiWallet/comments/qt0mgz/crashing_on_open/
				// https://github.com/WalletWasabi/WalletWasabi/issues/5255
				Logger.LogInfo($"{nameof(AddressManager)} has thrown `{ex.GetType().Name}`. Attempting to autocorrect.");
				File.Delete(_addressManagerFilePath);
				Logger.LogTrace(ex);
				_addressManager = new AddressManager();
				Logger.LogInfo($"{nameof(AddressManager)} autocorrection is successful.");
			}

			var addressManagerBehavior = new AddressManagerBehavior(_addressManager)
			{
				Mode = needsToDiscoverPeers ? AddressManagerBehaviorMode.Discover : AddressManagerBehaviorMode.None
			};

			var userAgent = Constants.UserAgents.RandomElement(SecureRandom.Instance);
			var connectionParameters = new NodeConnectionParameters { UserAgent = userAgent };

			connectionParameters.TemplateBehaviors.Add(_bitcoinStore.CreateUntrustedP2pBehavior());
			connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);
			connectionParameters.EndpointConnector = new BestEffortEndpointConnector(MaximumNodeConnections / 2);

			if (torSocks5EndPoint is not null)
			{
				connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(torSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
			}
			var nodes = new NodesGroup(_network, connectionParameters, requirements: Constants.NodeRequirements);
			nodes.ConnectedNodes.Added += ConnectedNodes_OnAddedOrRemoved;
			nodes.ConnectedNodes.Removed += ConnectedNodes_OnAddedOrRemoved;
			nodes.MaximumNodeConnection = MaximumNodeConnections;

			Nodes = nodes;
		}
	}

	private readonly Network _network;
	private readonly EndPoint _fullNodeP2PEndPoint;
	private readonly BitcoinStore _bitcoinStore;
	public NodesGroup Nodes { get; }
	private Node? RegTestMempoolServingNode { get; set; }
	private readonly string _addressManagerFilePath;
	private readonly AddressManager _addressManager;

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (_network == Network.RegTest)
		{
			try
			{
				Node node = await Node.ConnectAsync(Network.RegTest, _fullNodeP2PEndPoint).ConfigureAwait(false);

				Nodes.ConnectedNodes.Add(node);

				RegTestMempoolServingNode = await Node.ConnectAsync(Network.RegTest, _fullNodeP2PEndPoint).ConfigureAwait(false);

				RegTestMempoolServingNode.Behaviors.Add(_bitcoinStore.CreateUntrustedP2pBehavior());
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
		IoHelpers.EnsureContainingDirectoryExists(_addressManagerFilePath);

		_addressManager.SavePeerFile(_addressManagerFilePath, _network);
		Logger.LogInfo($"{nameof(AddressManager)} is saved to `{_addressManagerFilePath}`.");

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
		if (_network != Network.RegTest)
		{
			Nodes.ConnectedNodes.Added -= ConnectedNodes_OnAddedOrRemoved;
			Nodes.ConnectedNodes.Removed -= ConnectedNodes_OnAddedOrRemoved;
		}

		Nodes.Dispose();
		base.Dispose();
	}
}

using Microsoft.Extensions.Hosting;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
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

namespace WalletWasabi.BitcoinP2p
{
	public class P2pNetwork : BackgroundService
	{
		public P2pNetwork(Network network, EndPoint fullnodeP2pEndPoint, EndPoint? torSocks5EndPoint, string workDir, BitcoinStore bitcoinStore)
		{
			Network = network;
			FullnodeP2PEndPoint = fullnodeP2pEndPoint;
			TorSocks5EndPoint = torSocks5EndPoint;
			WorkDir = workDir;
			BitcoinStore = bitcoinStore;

			var userAgent = Constants.UserAgents.RandomElement();
			var connectionParameters = new NodeConnectionParameters { UserAgent = userAgent };

			connectionParameters.TemplateBehaviors.Add(BitcoinStore.CreateUntrustedP2pBehavior());

			AddressManagerFilePath = Path.Combine(WorkDir, $"AddressManager{Network}.dat");
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
					AddressManager = AddressManager.LoadPeerFile(AddressManagerFilePath);

					// Most of the times we do not need to discover new peers. Instead, we can connect to
					// some of those that we already discovered in the past. In this case we assume that
					// discovering new peers could be necessary if our address manager has less
					// than 500 addresses. 500 addresses could be okay because previously we tried with
					// 200 and only one user reported he/she was not able to connect (there could be many others,
					// of course).
					// On the other side, increasing this number forces users that do not need to discover more peers
					// to spend resources (CPU/bandwidth) to discover new peers.
					needsToDiscoverPeers = TorSocks5EndPoint is not null || AddressManager.Count < 500;
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

			connectionParameters.TemplateBehaviors.Add(addressManagerBehavior);

			if (Network == Network.RegTest)
			{
				Nodes = new NodesGroup(Network, requirements: Constants.NodeRequirements);
			}
			else
			{
				var maximumNodeConnection = 12;
				var bestEffortEndpointConnector = new BestEffortEndpointConnector(maximumNodeConnection / 2);
				connectionParameters.EndpointConnector = bestEffortEndpointConnector;
				if (TorSocks5EndPoint is not null)
				{
					connectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(TorSocks5EndPoint, onlyForOnionHosts: false, networkCredential: null, streamIsolation: true));
				}
				var nodes = new NodesGroup(Network, connectionParameters, requirements: Constants.NodeRequirements);
				nodes.ConnectedNodes.Added += ConnectedNodes_OnAddedOrRemoved;
				nodes.ConnectedNodes.Removed += ConnectedNodes_OnAddedOrRemoved;
				nodes.MaximumNodeConnection = maximumNodeConnection;

				Nodes = nodes;
			}
		}

		public Network Network { get; }
		public EndPoint FullnodeP2PEndPoint { get; }
		public EndPoint? TorSocks5EndPoint { get; }
		public string WorkDir { get; }
		public BitcoinStore BitcoinStore { get; }
		public NodesGroup Nodes { get; }
		private Node? RegTestMempoolServingNode { get; set; }
		private string? AddressManagerFilePath { get; set; }
		private AddressManager? AddressManager { get; set; }

		/// <inheritdoc />
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			if (Network == Network.RegTest)
			{
				try
				{
					EndPoint bitcoinCoreEndpoint = FullnodeP2PEndPoint;

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
			if (AddressManagerFilePath is { } addressManagerFilePath)
			{
				IoHelpers.EnsureContainingDirectoryExists(addressManagerFilePath);
				var addressManager = AddressManager;
				if (addressManager is { })
				{
					addressManager.SavePeerFile(AddressManagerFilePath, Network);
					Logger.LogInfo($"{nameof(AddressManager)} is saved to `{AddressManagerFilePath}`.");
				}
			}

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
			Nodes.ConnectedNodes.Added -= ConnectedNodes_OnAddedOrRemoved;
			Nodes.ConnectedNodes.Removed -= ConnectedNodes_OnAddedOrRemoved;
			Nodes.Dispose();
			base.Dispose();
		}
	}
}

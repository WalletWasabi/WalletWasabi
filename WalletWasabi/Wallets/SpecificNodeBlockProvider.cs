using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

public class SpecificNodeBlockProvider : IBlockProvider
{
	// This provider uses <Network>BitcoinP2pEndPoint from Config to provide blocks from a local or remote node.
	public SpecificNodeBlockProvider(Network network, ServiceConfiguration serviceConfiguration, HttpClientFactory httpClientFactory)
	{
		Network = network;
		ServiceConfiguration = serviceConfiguration;
		HttpClientFactory = httpClientFactory;
	}

	private Network Network { get; }
	private Node? SpecificBitcoinCoreNode { get; set; }
	private ServiceConfiguration ServiceConfiguration { get; }
	private HttpClientFactory HttpClientFactory { get; }

	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		var specificNodeEndPoint = ServiceConfiguration.BitcoinCoreEndPoint;
		try
		{
			if (SpecificBitcoinCoreNode is null || (!SpecificBitcoinCoreNode.IsConnected && Network != Network.RegTest)) // If RegTest then we're already connected do not try again.
			{
				DisconnectDisposeNulSpecificBitcoinCoreNode();
				using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
				var nodeConnectionParameters = new NodeConnectionParameters()
				{
					ConnectCancellation = handshakeTimeout.Token,
					IsRelay = false,
					UserAgent = $"/Wasabi:{Constants.ClientVersion}/"
				};

				// If an onion was added must try to use Tor.
				// onlyForOnionHosts should connect to it if it's an onion endpoint automatically and non-Tor endpoints through clearnet/localhost
				if (HttpClientFactory.IsTorEnabled)
				{
					nodeConnectionParameters.TemplateBehaviors.Add(new SocksSettingsBehavior(HttpClientFactory.TorEndpoint, onlyForOnionHosts: true, networkCredential: null, streamIsolation: false));
				}

				var node = await Node.ConnectAsync(Network, specificNodeEndPoint, nodeConnectionParameters).ConfigureAwait(false);
				try
				{
					Logger.LogInfo("TCP Connection succeeded, handshaking...");
					node.VersionHandshake(Constants.LocalNodeRequirements, handshakeTimeout.Token);
					var peerServices = node.PeerVersion.Services;

					Logger.LogInfo("Handshake completed successfully.");

					if (!node.IsConnected)
					{
						throw new InvalidOperationException($"Wasabi could not complete the handshake with the node at {specificNodeEndPoint} and dropped the connection.{Environment.NewLine}" +
							"Probably this is because the node does not support retrieving full blocks or segwit serialization.");
					}
					SpecificBitcoinCoreNode = node;
				}
				catch (OperationCanceledException) when (handshakeTimeout.IsCancellationRequested)
				{
					Logger.LogWarning($"Wasabi could not complete the handshake with the node at {specificNodeEndPoint}. Probably Wasabi is not whitelisted by the node.{Environment.NewLine}" +
						"Use \"whitebind\" in the node configuration. (Typically whitebind=127.0.0.1:8333 if Wasabi and the node are on the same machine and whitelist=1.2.3.4 if they are not.)");
					throw;
				}
			}

			// Get block from specific node.
			Block blockFromSpecificNode;
			
			// Should timeout faster. Not sure if it should ever fail though. Maybe let's keep like this later for remote node connection.
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(64)))
			{
				blockFromSpecificNode = await SpecificBitcoinCoreNode.DownloadBlockAsync(hash, cts.Token).ConfigureAwait(false);
			}

			// Validate retrieved block.
			if (!blockFromSpecificNode.Check())
			{
				throw new InvalidOperationException("Disconnected node, because invalid block received!");
			}

			// Retrieved block from specific node and block is valid.
			Logger.LogInfo($"Block {hash} acquired from node at {specificNodeEndPoint}.");
			return blockFromSpecificNode;
		}
		catch (Exception ex)
		{
			DisconnectDisposeNulSpecificBitcoinCoreNode();

			if (ex is SocketException)
			{
				Logger.LogTrace($"Did not find a full node instance running and listening at {specificNodeEndPoint}");
			}
			else
			{
				Logger.LogWarning(ex);
			}
		}
		return null;
	}

	private void DisconnectDisposeNulSpecificBitcoinCoreNode()
	{
		if (SpecificBitcoinCoreNode is { })
		{
			try
			{
				SpecificBitcoinCoreNode?.Disconnect();
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
			finally
			{
				try
				{
					SpecificBitcoinCoreNode?.Dispose();
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
				finally
				{
					SpecificBitcoinCoreNode = null;
					Logger.LogInfo($"Node specified in config was disconnected.");
				}
			}
		}
	}
}

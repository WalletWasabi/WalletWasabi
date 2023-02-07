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

public class LocalBlockProvider : IBlockProvider
{

	public LocalBlockProvider(Network network, ServiceConfiguration serviceConfiguration, HttpClientFactory httpClientFactory)
	{
		Network = network;
		ServiceConfiguration = serviceConfiguration;
		HttpClientFactory = httpClientFactory;
	}

	private Network Network { get; }
	private Node? LocalBitcoinCoreNode { get; set; }
	private ServiceConfiguration ServiceConfiguration { get; }
	private HttpClientFactory HttpClientFactory { get; }

	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		try
		{
			if (LocalBitcoinCoreNode is null || (!LocalBitcoinCoreNode.IsConnected && Network != Network.RegTest)) // If RegTest then we're already connected do not try again.
			{
				DisconnectDisposeNullLocalBitcoinCoreNode();
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

				var localEndPoint = ServiceConfiguration.BitcoinCoreEndPoint;
				var localNode = await Node.ConnectAsync(Network, localEndPoint, nodeConnectionParameters).ConfigureAwait(false);
				try
				{
					Logger.LogInfo("TCP Connection succeeded, handshaking...");
					localNode.VersionHandshake(Constants.LocalNodeRequirements, handshakeTimeout.Token);
					var peerServices = localNode.PeerVersion.Services;

					Logger.LogInfo("Handshake completed successfully.");

					if (!localNode.IsConnected)
					{
						throw new InvalidOperationException($"Wasabi could not complete the handshake with the local node and dropped the connection.{Environment.NewLine}" +
							"Probably this is because the node does not support retrieving full blocks or segwit serialization.");
					}
					LocalBitcoinCoreNode = localNode;
				}
				catch (OperationCanceledException) when (handshakeTimeout.IsCancellationRequested)
				{
					Logger.LogWarning($"Wasabi could not complete the handshake with the local node. Probably Wasabi is not whitelisted by the node.{Environment.NewLine}" +
						"Use \"whitebind\" in the node configuration. (Typically whitebind=127.0.0.1:8333 if Wasabi and the node are on the same machine and whitelist=1.2.3.4 if they are not.)");
					throw;
				}
			}

			// Get block from local node.
			Block blockFromLocalNode;
			// Should timeout faster. Not sure if it should ever fail though. Maybe let's keep like this later for remote node connection.
			using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(64)))
			{
				blockFromLocalNode = await LocalBitcoinCoreNode.DownloadBlockAsync(hash, cts.Token).ConfigureAwait(false);
			}

			// Validate retrieved block.
			if (!blockFromLocalNode.Check())
			{
				throw new InvalidOperationException("Disconnected node, because invalid block received!");
			}

			// Retrieved block from local node and block is valid.
			Logger.LogInfo($"Block acquired from local P2P connection: {hash}.");
			return blockFromLocalNode;
		}
		catch (Exception ex)
		{
			DisconnectDisposeNullLocalBitcoinCoreNode();

			if (ex is SocketException)
			{
				Logger.LogTrace("Did not find local listening and running full node instance. Trying to fetch needed block from other source.");
			}
			else
			{
				Logger.LogWarning(ex);
			}
		}
		return null;
	}

	private void DisconnectDisposeNullLocalBitcoinCoreNode()
	{
		if (LocalBitcoinCoreNode is { })
		{
			try
			{
				LocalBitcoinCoreNode?.Disconnect();
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
			finally
			{
				try
				{
					LocalBitcoinCoreNode?.Dispose();
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
				}
				finally
				{
					LocalBitcoinCoreNode = null;
					Logger.LogInfo($"Local {Constants.BuiltinBitcoinNodeName} node disconnected.");
				}
			}
		}
	}
}

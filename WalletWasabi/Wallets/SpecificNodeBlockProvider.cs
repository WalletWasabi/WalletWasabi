using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets.BlockProvider;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Wallets;

/// <summary>
/// This block provider uses <c>&lt;Network&gt;BitcoinP2pEndPoint</c> from Wasabi Wallet config to provide blocks from a local or remote node.
/// </summary>
public class SpecificNodeBlockProvider : IBlockProvider, IAsyncDisposable
{
	private static TimeSpan MinReconnectDelay = TimeSpan.FromSeconds(1);
	private static TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

	private volatile ConnectedNode? _specificBitcoinCoreNode;

	public SpecificNodeBlockProvider(Network network, ServiceConfiguration serviceConfiguration, HttpClientFactory httpClientFactory)
	{
		Network = network;
		BitcoinCoreEndPoint = serviceConfiguration.BitcoinCoreEndPoint;
		HttpClientFactory = httpClientFactory;

		// Start the task now.
		LoopTask = Task.Run(ReconnectingLoopAsync);
	}

	/// <summary>To stop the loop that keeps connecting to the specific Bitcoin node.</summary>
	private CancellationTokenSource LoopCts { get; } = new();
	private Task LoopTask { get; }
	private Network Network { get; }
	private EndPoint BitcoinCoreEndPoint { get; }
	private HttpClientFactory HttpClientFactory { get; }

	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		if (_specificBitcoinCoreNode is { } node)
		{
			Block block;

			// Should timeout faster. Not sure if it should ever fail though. Maybe let's keep like this later for remote node connection.
			using (CancellationTokenSource cts = new(TimeSpan.FromSeconds(64)))
			{
				block = await node.Node.DownloadBlockAsync(hash, cts.Token).ConfigureAwait(false);
			}

			// Validate retrieved block.
			if (!block.Check())
			{
				// Block is invalid. There is not much we can do.
				Logger.LogInfo($"Block {hash} provided by node '{node.Node}' is invalid. Is the node trusted?");
				return null;
			}

			// Retrieved block from specific node and block is valid.
			Logger.LogInfo($"Block {hash} acquired from node '{node.Node}'.");

			return block;
		}

		return null;
	}

	/// <summary>
	/// Keep connecting to the specific Bitcoin Core node.
	/// </summary>
	private async Task ReconnectingLoopAsync()
	{
		CancellationToken shutdownToken = LoopCts.Token;
		TimeSpan reconnectDelay = MinReconnectDelay;

		while (!shutdownToken.IsCancellationRequested)
		{
			using CancellationTokenSource connectCts = new(TimeSpan.FromSeconds(10));
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(connectCts.Token, shutdownToken);

			_specificBitcoinCoreNode = null;

			NodeConnectionParameters nodeConnectionParameters = new()
			{
				ConnectCancellation = linkedCts.Token,
				IsRelay = false,
				UserAgent = $"/Wasabi:{Constants.ClientVersion}/"
			};

			// If an onion was added must try to use Tor.
			// onlyForOnionHosts should connect to it if it's an onion endpoint automatically and non-Tor endpoints through clearnet/localhost
			if (HttpClientFactory.IsTorEnabled)
			{
				SocksSettingsBehavior behavior = new(HttpClientFactory.TorEndpoint, onlyForOnionHosts: true, networkCredential: null, streamIsolation: false);
				nodeConnectionParameters.TemplateBehaviors.Add(behavior);
			}

			try
			{
				// Connect to the node.
				Node localNode = await Node.ConnectAsync(Network, BitcoinCoreEndPoint, nodeConnectionParameters).ConfigureAwait(false);
				Logger.LogInfo("TCP Connection succeeded, handshaking…");

				try
				{
					localNode.VersionHandshake(Constants.LocalNodeRequirements, linkedCts.Token);
					Logger.LogInfo("Handshake completed successfully.");

					if (!localNode.IsConnected)
					{
						throw new InvalidOperationException($"Wasabi could not complete the handshake with the node '{BitcoinCoreEndPoint}' and dropped the connection.{Environment.NewLine}" +
							"Probably this is because the node does not support retrieving full blocks or segwit serialization.");
					}
				}
				catch (OperationCanceledException) when (connectCts.Token.IsCancellationRequested)
				{
					string message = $"""
						Wasabi could not complete the handshake with the node '{BitcoinCoreEndPoint}'. Probably Wasabi is not whitelisted by the node.
						Use "whitebind" in the node configuration. Typically whitebind=127.0.0.1:8333 if Wasabi and the node are on the same machine and whitelist=1.2.3.4 if they are not.
						""";

					Logger.LogWarning(message);
					throw;
				}

				// Reset reconnect delay as we actually connected the local node.
				reconnectDelay = MinReconnectDelay;

				using ConnectedNode connectedNode = new(localNode);
				_specificBitcoinCoreNode = connectedNode;

				_ = await connectedNode.WaitUntilDisconnectedAsync(shutdownToken).ConfigureAwait(false);

				if (shutdownToken.IsCancellationRequested)
				{
					break;
				}
			}
			catch (Exception ex) when (!shutdownToken.IsCancellationRequested)
			{
				Logger.LogTrace("Failed to establish connection to the local Bitcoin Core node.", ex);

				// Failing to connect leads to exponential slowdown.
				reconnectDelay *= 2;

				if (reconnectDelay > MaxReconnectDelay)
				{
					reconnectDelay = MaxReconnectDelay;
				}
			}
			catch (Exception) when (shutdownToken.IsCancellationRequested)
			{
				Logger.LogTrace("Operation stopped by user.");
				break;
			}
			finally
			{
				_specificBitcoinCoreNode = null;
			}

			// Wait for the next attempt to connect.
			try
			{
				await Task.Delay(reconnectDelay, shutdownToken).ConfigureAwait(false);
			}
			catch
			{
				// The loop is ending now.
				break;
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		LoopCts.Cancel();

		Logger.LogDebug("Waiting for the loop task to finish.");
		await LoopTask.ConfigureAwait(false);

		LoopCts.Dispose();
	}
}

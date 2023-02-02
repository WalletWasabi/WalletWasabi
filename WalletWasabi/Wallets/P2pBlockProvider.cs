using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Linq;
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

/// <summary>
/// P2P block provider is a blocks provider getting the blocks from bitcoin nodes using the P2P bitcoin protocol.
/// </summary>
public class P2pBlockProvider : IBlockProvider
{
	public P2pBlockProvider(NodesGroup nodes, HttpClientFactory httpClientFactory, Network network)
	{
		Nodes = nodes;
		HttpClientFactory = httpClientFactory;
		Network = network;
	}

	private NodesGroup Nodes { get; }
	private HttpClientFactory HttpClientFactory { get; }
	private Network Network { get; }

	private int NodeTimeouts { get; set; }

	/// <summary>
	/// Gets a bitcoin block from bitcoin nodes using the P2P bitcoin protocol.
	/// If a bitcoin node is available it fetches the blocks using the RPC interface.
	/// </summary>
	/// <param name="hash">The block's hash that identifies the requested block.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The requested bitcoin block.</returns>
	public async Task<Block?> TryGetBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		Block? block;

		while (true)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				// If no connection, wait, then continue.
				while (Nodes.ConnectedNodes.Count == 0)
				{
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
				}

				// Select a random node we are connected to.
				Node? node = Nodes.ConnectedNodes.RandomElement();
				if (node is null || !node.IsConnected)
				{
					await Task.Delay(100, cancellationToken).ConfigureAwait(false);
					continue;
				}

				// Download block from selected node.
				try
				{
					// More permissive timeout if few nodes are connected to avoid exhaustion
					var timeout = Nodes.ConnectedNodes.Count < 3
						? Math.Min(RuntimeParams.Instance.NetworkNodeTimeout * 1.5, 600)
						: RuntimeParams.Instance.NetworkNodeTimeout;

					using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout)))
					{
						using var lts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
						block = await node.DownloadBlockAsync(hash, lts.Token).ConfigureAwait(false);
					}

					// Validate block
					if (!block.Check())
					{
						DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because invalid block received.", force: true);
						continue;
					}

					DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}. Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");

					await NodeTimeoutsAsync(increaseDecrease: false).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
				{
					await NodeTimeoutsAsync(increaseDecrease: true).ConfigureAwait(false);

					DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download took too long."); // it could be a slow connection and not a misbehaving node
					continue;
				}
				catch (Exception ex)
				{
					Logger.LogDebug(ex);
					DisconnectNode(node, $"Disconnected node: {node.RemoteSocketAddress}, because block download failed: {ex.Message}.", force: true);
					continue;
				}

				break; // If got this far, then we have the block and it's valid. Break.
			}
			catch (Exception ex)
			{
				Logger.LogDebug(ex);
			}
		}

		return block;
	}

	private void DisconnectNode(Node node, string logIfDisconnect, bool force = false)
	{
		if (Nodes.ConnectedNodes.Count > 3 || force)
		{
			Logger.LogInfo(logIfDisconnect);
			node.DisconnectAsync(logIfDisconnect);
		}
	}

	/// <summary>
	/// Current timeout used when downloading a block from the remote node. It is defined in seconds.
	/// </summary>
	private async Task NodeTimeoutsAsync(bool increaseDecrease)
	{
		if (increaseDecrease)
		{
			NodeTimeouts++;
		}
		else
		{
			NodeTimeouts--;
		}

		var timeout = RuntimeParams.Instance.NetworkNodeTimeout;

		// If it times out 2 times in a row then increase the timeout.
		if (NodeTimeouts >= 2)
		{
			NodeTimeouts = 0;
			timeout = (int)Math.Round(timeout * 1.5);
		}
		else if (NodeTimeouts <= -3) // If it does not time out 3 times in a row, lower the timeout.
		{
			NodeTimeouts = 0;
			timeout = (int)Math.Round(timeout * 0.7);
		}

		// Sanity check
		var minTimeout = Network == Network.Main ? 3 : 2;
		minTimeout = HttpClientFactory.IsTorEnabled ? (int)Math.Round(minTimeout * 1.5) : minTimeout;
		if (timeout < minTimeout)
		{
			timeout = minTimeout;
		}
		else if (timeout > 600)
		{
			timeout = 600;
		}

		if (timeout == RuntimeParams.Instance.NetworkNodeTimeout)
		{
			return;
		}
		RuntimeParams.Instance.NetworkNodeTimeout = timeout;
		await RuntimeParams.Instance.SaveAsync().ConfigureAwait(false);

		Logger.LogInfo($"Current timeout value used on block download is: {timeout} seconds.");
	}
}

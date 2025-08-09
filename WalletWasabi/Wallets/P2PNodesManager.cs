using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

public class P2PNodesManager
{
	public P2PNodesManager(Network network, NodesGroup nodes)
	{
		_network = network;
		_nodes = nodes;
	}

	private readonly Network _network;
	private readonly NodesGroup _nodes;
	private int _timeoutsCounter;
	private int _currentTimeoutSeconds = 16;

	public async Task<Node> GetNodeForSingleUseAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			if (_nodes.ConnectedNodes.Count == 0)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
				continue;
			}

			var node = _nodes.ConnectedNodes.RandomElement(SecureRandom.Instance);

			if (node is not null && node.IsConnected)
			{
				return node;
			}
			Logger.LogTrace($"Selected node is null or disconnected.");

			await Task.Delay(10, cancellationToken).ConfigureAwait(false);
		}

		cancellationToken.ThrowIfCancellationRequested();
		throw new InvalidOperationException("Failed to retrieve a connected node.");
	}

	public void DisconnectNodeIfEnoughPeers(Node node, string reason)
	{
		if (_nodes.ConnectedNodes.Count > 5)
		{
			DisconnectNode(node, reason);
		}
	}

	public void DisconnectNode(Node node, string reason)
	{
		Logger.LogInfo(reason);
		node.DisconnectAsync(reason);
	}

	public double GetCurrentTimeout()
	{
		// More permissive timeout if few nodes are connected to avoid exhaustion.
		return _nodes.ConnectedNodes.Count < 3
			? Math.Min(_currentTimeoutSeconds * 1.5, 600)
			: _currentTimeoutSeconds;
	}

	/// <summary>
	/// Current timeout used when downloading a block from the remote node. It is defined in seconds.
	/// </summary>
	public async Task UpdateTimeoutAsync(bool increaseDecrease)
	{
		if (increaseDecrease)
		{
			_timeoutsCounter++;
		}
		else
		{
			_timeoutsCounter--;
		}

		var timeout = _currentTimeoutSeconds;

		// If it times out 2 times in a row then increase the timeout.
		if (_timeoutsCounter >= 2)
		{
			_timeoutsCounter = 0;
			timeout = (int)Math.Round(timeout * 1.5);
		}
		else if (_timeoutsCounter <= -3) // If it does not time out 3 times in a row, lower the timeout.
		{
			_timeoutsCounter = 0;
			timeout = (int)Math.Round(timeout * 0.7);
		}

		// Sanity check
		var minTimeout = _network == Network.Main ? 3 : 2;

		if (timeout < minTimeout)
		{
			timeout = minTimeout;
		}
		else if (timeout > 600)
		{
			timeout = 600;
		}

		_currentTimeoutSeconds = timeout;
		Logger.LogInfo($"Current timeout value used on block download is: {timeout} seconds.");
	}
}

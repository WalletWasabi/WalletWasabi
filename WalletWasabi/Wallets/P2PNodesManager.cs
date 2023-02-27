using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Wallets;

public class P2PNodesManager
{
	public P2PNodesManager(Network network, NodesGroup nodes, bool isTorEnabled)
	{
		Network = network;
		Nodes = nodes;
		IsTorEnabled = isTorEnabled;
	}
	
	private Network Network { get; }
	private NodesGroup Nodes { get; }
	private bool IsTorEnabled { get; }
	private int NodeTimeouts { get; set; }
	
	public async Task<Node?> GetNodeAsync(CancellationToken cancellationToken)
	{
		while (Nodes.ConnectedNodes.Count == 0)
		{
			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
		}

		// Select a random node we are connected to.
		return Nodes.ConnectedNodes.RandomElement();
	}
	
	public void DisconnectNode(Node node, string logIfDisconnect, bool force = false)
	{
		if (Nodes.ConnectedNodes.Count > 3 || force)
		{
			Logger.LogInfo(logIfDisconnect);
			node.DisconnectAsync(logIfDisconnect);
		}
	}

	public double GetCurrentTimeout()
	{
		// More permissive timeout if few nodes are connected to avoid exhaustion
		return Nodes.ConnectedNodes.Count < 3
			? Math.Min(RuntimeParams.Instance.NetworkNodeTimeout * 1.5, 600)
			: RuntimeParams.Instance.NetworkNodeTimeout;
	}
	
	/// <summary>
	/// Current timeout used when downloading a block from the remote node. It is defined in seconds.
	/// </summary>
	public async Task UpdateTimeoutAsync(bool increaseDecrease)
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
		minTimeout = IsTorEnabled ? (int)Math.Round(minTimeout * 1.5) : minTimeout;

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

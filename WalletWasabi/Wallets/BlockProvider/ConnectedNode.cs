using NBitcoin.Protocol;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets.BlockProvider;

public class ConnectedNode : IDisposable
{
	public ConnectedNode(Node node)
	{
		Node = node;
		node.StateChanged += Node_StateChanged;
	}

	public Node Node { get; }
	private CancellationTokenSource DisconnectedCts { get; } = new();

	private void Node_StateChanged(Node node, NodeState oldState)
	{
		if (!node.IsConnected)
		{
			DisconnectedCts.Cancel();
		}
	}

	/// <summary>
	/// Best-effort disconnect.
	/// </summary>
	public void Disconnect()
	{
		try
		{
			Node.Disconnect();
		}
		catch
		{
		}
	}

	/// <summary>
	/// Waits until the node gets disconnected.
	/// </summary>
	/// <returns><c>true</c> if the node got disconnected, <c>false</c> if the operation was cancelled by the user.</returns>
	public async Task<bool> WaitUntilDisconnectedAsync(CancellationToken cancellationToken)
	{
		try
		{
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisconnectedCts.Token);
			await Task.Delay(Timeout.Infinite, linkedCts.Token).ConfigureAwait(false);

			throw new InvalidOperationException("Unreachable code.");
		}
		catch (OperationCanceledException)
		{
			// Intentionally implemented like this as cancelling by user has higher priority than node being disconnected.
			return !cancellationToken.IsCancellationRequested;
		}
	}

	public void Dispose()
	{
		Node.StateChanged -= Node_StateChanged;
		DisconnectedCts.Dispose();
	}
}

using NBitcoin;
using NBitcoin.Protocol;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;

namespace WalletWasabi.Wallets.BlockProvider;

public class ConnectedNode : IDisposable
{
	public ConnectedNode(Node node)
	{
		Name = node.ToString();
		Node = node;
		node.StateChanged += Node_StateChanged;
		DisconnectedCts = new();
	}

	/// <remarks>For testing purposes.</remarks>
	internal ConnectedNode(CancellationTokenSource disconnectedCts)
	{
		Name = "Test node";
		Node = null!;
		DisconnectedCts = disconnectedCts;
	}

	public string Name { get; }
	private Node Node { get; }
	private CancellationTokenSource DisconnectedCts { get; }

	private void Node_StateChanged(Node node, NodeState oldState)
	{
		if (!node.IsConnected)
		{
			DisconnectedCts.Cancel();
		}
	}

	public virtual Task<Block> DownloadBlockAsync(uint256 hash, CancellationToken cancellationToken)
	{
		return Node.DownloadBlockAsync(hash, cancellationToken);
	}

	/// <summary>
	/// Waits until the node gets disconnected.
	/// </summary>
	/// <returns><c>true</c> if the node got disconnected, <c>false</c> if the operation was cancelled by the user.</returns>
	public virtual async Task<bool> WaitUntilDisconnectedAsync(CancellationToken cancellationToken)
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

	/// <inheritdoc/>
	public override string? ToString()
	{
		return Name;
	}

	public void Dispose()
	{
		if (Node is not null)
		{
			Node.StateChanged -= Node_StateChanged;
			Node.Dispose();
		}

		DisconnectedCts.Dispose();
	}
}

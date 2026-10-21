using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Logging;

namespace WalletWasabi.Coordinator;

/// <summary>
/// Keeps the coordinator connected to its own bitcoin node, which relays every transaction entering its mempool.
/// </summary>
public class P2pNode(Network network, EndPoint endPoint, MempoolService mempoolService) : PeriodicRunner(TimeSpan.FromSeconds(10))
{
	private Node? _node;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		if (_node is { State: NodeState.HandShaked })
		{
			return;
		}

		_node?.Dispose();

		NodeConnectionParameters parameters = new()
		{
			ConnectCancellation = cancel,
			IsRelay = true
		};
		parameters.TemplateBehaviors.Add(new P2pBehavior(mempoolService));

		_node = await Node.ConnectAsync(network, endPoint, parameters).ConfigureAwait(false);
		await _node.VersionHandshakeAsync(cancel).ConfigureAwait(false);
		Logger.LogInfo($"Connected to the bitcoin node at {endPoint}.");
	}

	public override void Dispose()
	{
		_node?.Dispose();
		base.Dispose();
	}
}

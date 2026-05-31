using System.Net;
using NBitcoin.Protocol;

namespace WalletWasabi.Services.NodesManagement;

public sealed record PeerInfo
{
	public required EndPoint Endpoint { get; init; }
	public required string UserAgent { get; init; }
	public required uint ProtocolVersion { get; init; }
	public required NodeServices Services { get; init; }
	public required int StartHeight { get; init; }
	public required TimeSpan ConnectionTime { get; init; }
	public required DateTimeOffset DiscoveredAt { get; init; }
	public DateTimeOffset LastSeen { get; init; }
	public int SuccessfulProbes { get; init; } = 1;
	public int FailedProbes { get; init; }
	public int QuickDisconnects { get; init; }

	public bool SupportsCompactFilters => Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS);
	public bool SupportsFullBlocks => Services.HasFlag(NodeServices.Network);
	public bool SupportsWitness => Services.HasFlag(NodeServices.NODE_WITNESS);
	public bool SupportsBlocksLimited => Services.HasFlag(NodeServices.NODE_NETWORK_LIMITED);

	public double Score => ComputeScore();

	private double ComputeScore()
	{
		double score =
			(Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS) ? 30 : 0) +
			(Services.HasFlag(NodeServices.Network) ? 20 : -10) +
			(Services.HasFlag(NodeServices.NODE_WITNESS) ? 5 : 0);

		var total = SuccessfulProbes + FailedProbes;
		if (total > 0)
		{
			score += (double)SuccessfulProbes / total * 15;
		}

		score -= QuickDisconnects * 10;

		return score;
	}
}

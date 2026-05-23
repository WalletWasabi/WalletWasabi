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
	public DateTimeOffset LastSeen { get; set; }
	public int SuccessfulProbes { get; init; } = 1;
	public int FailedProbes { get; init; }
	public int QuickDisconnects { get; init; }

	public bool SupportsCompactFilters => Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS);
	public bool SupportsFullBlocks => Services.HasFlag(NodeServices.Network);
	public bool SupportsWitness => Services.HasFlag(NodeServices.NODE_WITNESS);
	public bool SupportsBlocksLimited => Services.HasFlag(NodeServices.NODE_NETWORK_LIMITED);

	private bool IsOnion => Endpoint is DnsEndPoint dns &&
	                        dns.Host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase);

	public double ComputeScore()
	{
		double score =
			(Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS) ? 15 : 0) +
			(Services.HasFlag(NodeServices.Network) ? 20 : -10) +
			(Services.HasFlag(NodeServices.NODE_WITNESS) ? 5 : 0);

		if (IsOnion)
		{
			score += 5;
		}

		var total = SuccessfulProbes + FailedProbes;
		if (total > 0)
		{
			score += (double)SuccessfulProbes / total * 15;
		}

		score -= QuickDisconnects * 10;

		return score;
	}
}

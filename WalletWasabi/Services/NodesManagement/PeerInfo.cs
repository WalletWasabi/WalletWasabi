using System.Diagnostics.CodeAnalysis;
using System.Net;
using NBitcoin.Protocol;

namespace WalletWasabi.Services.NodesManagement;

public sealed record PeerInfo
{
	[SetsRequiredMembers]
	public PeerInfo(EndPoint endpoint, string userAgent, uint protocolVersion, NodeServices services, int startHeight, TimeSpan connectionTime, DateTimeOffset discoveredAt, DateTimeOffset lastSeen)
	{
		Endpoint = endpoint;
		UserAgent = userAgent;
		ProtocolVersion = protocolVersion;
		Services = services;
		StartHeight = startHeight;
		ConnectionTime = connectionTime;
		DiscoveredAt = discoveredAt;
		LastSeen = lastSeen;
		Score = ComputeScore();
	}

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

	public bool SupportsCompactFilters => Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS);
	public bool SupportsFullBlocks => Services.HasFlag(NodeServices.Network);
	public bool SupportsWitness => Services.HasFlag(NodeServices.NODE_WITNESS);
	public bool SupportsBlocksLimited => Services.HasFlag(NodeServices.NODE_NETWORK_LIMITED);

	public double Score { get; init; }

	private double ComputeScore() =>
		(SupportsCompactFilters ? 30 : 0) +
		(SupportsFullBlocks ? 20 : -10) +
		(SupportsWitness ? 5 : 0);
}

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies;

public record CredentialEdgeSet
{
	public CredentialType CredentialType { get; init; }

	public long MaxCredentialValue { get; init; }

	public ImmutableDictionary<RequestNode, ImmutableHashSet<CredentialDependency>> Predecessors { get; init; } = ImmutableDictionary.Create<RequestNode, ImmutableHashSet<CredentialDependency>>();
	public ImmutableDictionary<RequestNode, ImmutableHashSet<CredentialDependency>> Successors { get; init; } = ImmutableDictionary.Create<RequestNode, ImmutableHashSet<CredentialDependency>>();
	public ImmutableDictionary<RequestNode, long> EdgeBalances { get; init; } = ImmutableDictionary.Create<RequestNode, long>();

	public long Balance(RequestNode node) => node.InitialBalance(CredentialType) + EdgeBalances[node];

	public ImmutableHashSet<CredentialDependency> InEdges(RequestNode node) => Predecessors[node];

	public ImmutableHashSet<CredentialDependency> OutEdges(RequestNode node) => Successors[node];

	public int InDegree(RequestNode node) => InEdges(node).Count;

	public int OutDegree(RequestNode node) => OutEdges(node).Count(x => x.Value != 0);

	public int ZeroOnlyOutDegree(RequestNode node) => OutEdges(node).Count(x => x.Value == 0);

	public int RemainingInDegree(RequestNode node) => node.MaxInDegree - InDegree(node);

	public int RemainingOutDegree(RequestNode node) => node.MaxOutDegree - OutDegree(node);

	public int RemainingZeroOnlyOutDegree(RequestNode node) => node.MaxZeroOnlyOutDegree - ZeroOnlyOutDegree(node);

	public int AvailableZeroOutDegree(RequestNode node) => RemainingZeroOnlyOutDegree(node) + (RemainingOutDegree(node) - (Balance(node) > 0 ? 1 : 0));

	public CredentialEdgeSet AddEdge(RequestNode from, RequestNode to, long value)
	{
		var edge = new CredentialDependency(from, to, CredentialType, Guard.MinimumAndNotNull(nameof(value), value, 0));

		// Maintain degree invariant (subset of K-regular graph, sort of)
		if (RemainingInDegree(edge.To) == 0)
		{
			throw new InvalidOperationException("Can't add more than k in edges per node.");
		}

		if (value > 0)
		{
			if (RemainingOutDegree(edge.From) == 0)
			{
				throw new InvalidOperationException("Can't add more than k non-zero out edges per node.");
			}
			else if (RemainingOutDegree(edge.From) == 1)
			{
				// This is the final out edge for the node edge.From
				if (Balance(edge.From) - edge.Value > 0)
				{
					throw new InvalidOperationException($"Can't add final out edge without discharging positive value (edge value {edge.Value} but node balance is {Balance(edge.From)}).");
				}

				// If it's the final edge overall for that node, the final balance must be 0
				if (RemainingInDegree(edge.From) == 0 && Balance(edge.From) - edge.Value != 0)
				{
					throw new InvalidOperationException("Can't add final in edge without discharging negative value completely.");
				}
			}
		}
		else
		{
			// For reissuance we can utilize all out edges.
			// For input nodes we may need one slot unutilized for the
			// remaining amount
			if (AvailableZeroOutDegree(edge.From) == 0)
			{
				throw new InvalidOperationException("Can't add more than 2k zero/non-zero out edge per node.");
			}
		}

		// Maintain balance sum invariant (initial balance and edge values cancel out)
		if (RemainingInDegree(edge.To) == 1)
		{
			// This is the final in edge for the node edge.To
			if (Balance(edge.To) + edge.Value < 0)
			{
				throw new InvalidOperationException("Can't add final in edge without discharging negative value.");
			}

			// If it's the final edge overall for that node, the final balance must be 0
			if (RemainingOutDegree(edge.To) == 0 && Balance(edge.To) + edge.Value != 0)
			{
				throw new InvalidOperationException("Can't add final in edge without discharging negative value completely.");
			}
		}

		if (RemainingOutDegree(edge.To) == 0)
		{
			if (Balance(edge.To) + edge.Value > 0)
			{
				throw new InvalidOperationException("Can't add edge with excess value to node with no remaining out degree.");
			}
		}

		var predecessors = InEdges(edge.To);
		var successors = OutEdges(edge.From);

		return this with
		{
			Predecessors = Predecessors.SetItem(edge.To, predecessors.Add(edge)),
			Successors = Successors.SetItem(edge.From, successors.Add(edge)),
			EdgeBalances = EdgeBalances.SetItems(
				new KeyValuePair<RequestNode, long>[]
				{
						new (edge.From, EdgeBalances[edge.From] - edge.Value),
						new (edge.To,   EdgeBalances[edge.To]   + edge.Value),
				}),
		};
	}

	// Find the largest negative or positive balance node for the given
	// credential type, and one or more smaller nodes with a combined total
	// magnitude exceeding that of the largest magnitude node when possible.
	public (RequestNode largestMagnitudeNode, IEnumerable<RequestNode> smallMagnitudeNodes, bool fanIn) MatchNodesToDischarge(IEnumerable<RequestNode> nodesWithRemainingOutDegree, IEnumerable<RequestNode> nodesWithRemainingInDegree)
	{
		ImmutableArray<RequestNode> sources = nodesWithRemainingOutDegree
			.OrderByDescending(v => Balance(v))
			.ThenByDescending(v => RemainingOutDegree(v))
			.ThenByDescending(v => AvailableZeroOutDegree(v))
			.ToImmutableArray();

		ImmutableArray<RequestNode> sinks = nodesWithRemainingInDegree
			.OrderBy(v => Balance(v))
			.ThenByDescending(v => RemainingInDegree(v))
			.ToImmutableArray();

		var nSources = 1;
		var nSinks = 1;

		long SourcesSum() => sources.Take(nSources).Sum(v => Balance(v));
		long SinksSum() => sinks.Take(nSinks).Sum(v => Balance(v));
		long CompareSums() => SourcesSum().CompareTo(-1 * SinksSum());

		// We want to fully discharge the larger (in absolute magnitude) of
		// the two nodes, so we will add more nodes to the smaller one until
		// we can fully cover. At each step of the iteration we fully
		// discharge at least 2 nodes from the queue.
		var initialComparison = CompareSums();
		var fanIn = initialComparison == -1;

		if (initialComparison != 0 && SinksSum() != 0)
		{
			Action takeOneMore = fanIn ? () => nSources++ : () => nSinks++;

			// Take more nodes until the comparison sign changes or
			// we run out.
			while (initialComparison == CompareSums()
				   && (fanIn ? sources.Length - nSources > 0
							 : sinks.Length - nSinks > 0))
			{
				takeOneMore();
			}
		}

		var largestMagnitudeNode = (fanIn ? sinks.First() : sources.First());
		var smallMagnitudeNodes = (fanIn ? sources.Take(nSources).Reverse() : sinks.Take(nSinks)); // reverse positive values so we always proceed in order of increasing magnitude

		return (largestMagnitudeNode, smallMagnitudeNodes, fanIn);
	}

	// Drain values into a reissuance request (towards the center of the graph).
	public CredentialEdgeSet DrainReissuance(RequestNode reissuance, IEnumerable<RequestNode> nodes)
		// The amount for the edge is always determined by the discharged
		// nodes' values, since we only add reissuance nodes to reduce the
		// number of charged nodes overall.
		=> nodes.Aggregate(this, (edgeSet, node) => edgeSet.DrainReissuance(reissuance, node));

	private CredentialEdgeSet DrainReissuance(RequestNode reissuance, RequestNode node)
	{
		var value = Balance(node);

		if (Math.Abs(Balance(reissuance) + value) > MaxCredentialValue)
		{
			// Avoid creating graphs that cannot be executed due to range
			// proof constraints. Technically up to K * MaxCredentialValue
			// is possible, but being stricter keeps it simple by avoiding
			// edge cases, and is likely to be good enough in practice as
			// this only really applies to vsize credentials and those
			// should almost always have a significant surplus, so this
			// should result in few strictly unnecessary reissuance
			// requests overall.
			return this;
		}
		else if (value > 0)
		{
			return AddEdge(node, reissuance, value);
		}
		else if (value < 0)
		{
			return AddEdge(reissuance, node, (-1 * value)).AddZeroEdges(reissuance, node);
		}
		else if (InDegree(reissuance) == 0)
		{
			// Due to opportunistic draining of lower priority credential
			// types when defining a reissuance node for higher priority
			// ones, the amount is not guaranteed to be zero, avoid adding
			// such edges.
			// Always satisfy zero credential from new reissuance nodes
			// (it's guaranteed to be possible) to avoid crossing edges,
			// even if there's no balance to discharge.
			return AddZeroEdges(reissuance, node);
		}
		else
		{
			return this;
		}
	}

	public CredentialEdgeSet AddZeroEdges(RequestNode src, RequestNode dst)
	   => RemainingInDegree(dst) switch
	   {
		   0 => this,
		   _ => AddZeroEdge(src, dst).AddZeroEdges(src, dst),
	   };

	public CredentialEdgeSet AddZeroEdge(RequestNode src, RequestNode dst) => AddEdge(src, dst, 0);

	// Drain credential values between terminal nodes, cancelling out
	// opposite values by propagating forwards or backwards corresponding to
	// fan-in and fan-out dependency structure.
	public CredentialEdgeSet DrainTerminal(RequestNode node, IEnumerable<RequestNode> nodes)
		=> nodes.Aggregate(this, (edgeSet, otherNode) => edgeSet.DrainTerminal(node, otherNode));

	private CredentialEdgeSet DrainTerminal(RequestNode node, RequestNode dischargeNode)
	{
		long value = Balance(dischargeNode);

		if (value < 0)
		{
			// Fan out, discharge the entire balance, adding zero edges if
			// needed (might not be if the discharged node has already
			// received an input edge in a previous pass).
			return AddEdge(node, dischargeNode, Math.Min(Balance(node), -1 * value));
		}
		else if (value > 0)
		{
			// Fan in, draining zero credentials is never necessary.
			var edgeValue = Math.Min(-1 * Balance(node), value);
			if (edgeValue == value || RemainingOutDegree(dischargeNode) > 1)
			{
				return AddEdge(dischargeNode, node, edgeValue);
			}
			else
			{
				// Sometimes the last dischargeNode can't be handled in this
				// iteration because the amount requires a change value but
				// its remaining out degree is already 1, requiring the
				// exact value to be used.
				// Just skip it here and it will eventually become the
				// largest magnitude node if it's required, and get handled
				// by the negative node discharging loop.
				return this;
			}
		}
		else
		{
			return this;
		}
	}

	public CredentialEdgeSet DrainZeroCredentials(RequestNode src, RequestNode dst)
		=> (Balance(dst) != 0 || AvailableZeroOutDegree(src) == 0 || RemainingInDegree(dst) == 0) switch
		{
			true => this,
			false => AddZeroEdge(src, dst).DrainZeroCredentials(src, dst),
		};
}

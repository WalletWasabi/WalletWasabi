using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	record CredentialEdgeSet
	{
		public CredentialType CredentialType { get; init; }
		public ImmutableDictionary<RequestNode, ImmutableHashSet<CredentialDependency>> Predecessors { get; init; } = ImmutableDictionary.Create<RequestNode, ImmutableHashSet<CredentialDependency>>();
		public ImmutableDictionary<RequestNode, ImmutableHashSet<CredentialDependency>> Successors { get; init; } = ImmutableDictionary.Create<RequestNode, ImmutableHashSet<CredentialDependency>>();
		public ImmutableDictionary<RequestNode, long> EdgeBalances { get; init; } = ImmutableDictionary.Create<RequestNode, long>();

		public long Balance(RequestNode node) => node.InitialBalance(CredentialType) + EdgeBalances[node];

		public ImmutableHashSet<CredentialDependency> InEdges(RequestNode node) => Predecessors[node];

		public ImmutableHashSet<CredentialDependency> OutEdges(RequestNode node) => Successors[node];

		public int InDegree(RequestNode node) => InEdges(node).Count();

		public int OutDegree(RequestNode node) => OutEdges(node).Count();

		public int RemainingInDegree(RequestNode node) => node.InitialBalance(CredentialType) <= 0 ? DependencyGraph.K - InDegree(node) : 0;

		public int RemainingOutDegree(RequestNode node) => node.InitialBalance(CredentialType) >= 0 ? DependencyGraph.K - OutDegree(node) : 0;

		public IOrderedEnumerable<RequestNode> OrderByBalance(IEnumerable<RequestNode> nodes) => nodes.OrderByDescending(v => Balance(v));

		public CredentialEdgeSet AddEdge(RequestNode from, RequestNode to, ulong value)
		{
			if (value == 0)
			{
				throw new ArgumentException("can't create edge with 0 value");
			}

			var edge = new CredentialDependency(from, to, CredentialType, value);

			var predecessors = InEdges(edge.To);
			var successors = OutEdges(edge.From);

			// Maintain subset of K-regular graph invariant
			if (RemainingOutDegree(edge.From) == 0 || RemainingInDegree(edge.To) == 0)
			{
				throw new InvalidOperationException("Can't add more than k edges");
			}

			if (RemainingOutDegree(edge.From) == 1)
			{
				// This is the final out edge for the node edge.From
				if (Balance(edge.From) - (long)edge.Value > 0)
				{
					throw new InvalidOperationException("Can't add final out edge without discharging positive value");
				}

				// If it's the final edge overall for that node, the final balance must be 0
				if (RemainingInDegree(edge.From) == 0 && Balance(edge.From) - (long)edge.Value != 0)
				{
					throw new InvalidOperationException("Can't add final edge without discharging negative value completely");
				}
			}

			if (RemainingInDegree(edge.To) == 1)
			{
				// This is the final in edge for the node edge.To
				if (Balance(edge.To) + (long)edge.Value < 0)
				{
					throw new InvalidOperationException("Can't add final in edge without discharging negative value");
				}

				// If it's the final edge overall for that node, the final balance must be 0
				if (RemainingOutDegree(edge.To) == 0 && Balance(edge.To) + (long)edge.Value != 0)
				{
					throw new InvalidOperationException("Can't add final edge without discharging negative value completely");
				}
			}

			return this with
			{
				Predecessors = Predecessors.SetItem(edge.To, predecessors.Add(edge)),
				Successors = Successors.SetItem(edge.From, successors.Add(edge)),
				EdgeBalances = EdgeBalances.SetItems(
					new KeyValuePair<RequestNode, long>[]
					{
						new (edge.From, EdgeBalances[edge.From] - (long)edge.Value),
						new (edge.To,   EdgeBalances[edge.To]   + (long)edge.Value),
					}),
			};
		}

		// Find the largest negative or positive balance node for the given
		// credential type, and one or more smaller nodes with a combined total
		// magnitude exceeding that of the largest magnitude node when possible.
		public bool SelectNodesToDischarge(IEnumerable<RequestNode> nodes, out RequestNode? largestMagnitudeNode, out IEnumerable<RequestNode> smallMagnitudeNodes, out bool fanIn)
		{
			// Order the given of the graph based on their balances
			var ordered = OrderByBalance(nodes);
			ImmutableArray<RequestNode> positive = ordered.ThenBy(x => OutDegree(x)).Where(v => Balance(v) > 0).ToImmutableArray();
			ImmutableArray<RequestNode> negative = Enumerable.Reverse(ordered.ThenByDescending(x => InDegree(x)).Where(v => Balance(v) < 0)).ToImmutableArray();

			if (negative.Length == 0)
			{
				largestMagnitudeNode = null;
				smallMagnitudeNodes = new RequestNode[0];
				fanIn = false;
				return false;
			}

			var nPositive = 1;
			var nNegative = 1;

			IEnumerable<RequestNode> PositiveCandidates() => positive.Take(nPositive);
			IEnumerable<RequestNode> NegativeCandidates() => negative.Take(nNegative);
			long PositiveSum() => PositiveCandidates().Sum(x => Balance(x));
			long NegativeSum() => NegativeCandidates().Sum(x => Balance(x));
			long CompareSums() => PositiveSum().CompareTo(-1 * NegativeSum());

			// We want to fully discharge the larger (in absolute magnitude) of
			// the two nodes, so we will add more nodes to the smaller one until
			// we can fully cover. At each step of the iteration we fully
			// discharge at least 2 nodes from the queue.
			var initialComparison = CompareSums();
			fanIn = initialComparison == -1;

			if (initialComparison != 0)
			{
				Action takeOneMore = fanIn ? () => nPositive++ : () => nNegative++;

				// Take more nodes until the comparison sign changes or
				// we run out.
				while (initialComparison == CompareSums()
					   && (fanIn ? positive.Length - nPositive > 0
								 : negative.Length - nNegative > 0))
				{
					takeOneMore();
				}
			}

			largestMagnitudeNode = (fanIn ? negative.First() : positive.First());
			smallMagnitudeNodes = (fanIn ? positive.Take(nPositive).Reverse() : negative.Take(nNegative)); // reverse positive values so we always proceed in order of increasing magnitude

			return true;
		}

		// Drain values into a reissuance request (towards the center of the graph).
		public CredentialEdgeSet DrainReissuance(RequestNode reissuance, IEnumerable<RequestNode> nodes)
			// The amount for the edge is always determined by the dicharged
			// nodes' values, since we only add reissuance nodes to reduce the
			// number of charged nodes overall.
			=> nodes.Aggregate(this, (edgeSet, node) => edgeSet.DrainReissuance(reissuance, node));

		private CredentialEdgeSet DrainReissuance(RequestNode reissuance, RequestNode node)
		{
			// Due to opportunistic draining of lower priority credential
			// types when defining a reissuance node for higher priority
			// ones, the amount is not guaranteed to be zero, avoid adding
			// such edges.
			long value = Balance(node);

			if (value > 0)
			{
				return AddEdge(node, reissuance, (ulong)value);
			}
			else if (value < 0)
			{
				return AddEdge(reissuance, node, (ulong)(-1 * value));
			}
			else
			{
				return this;
			}
		}

		// Drain credential values between terminal nodes, cancelling out
		// opposite values by propagating forwards or backwards corresponding to
		// fan-in and fan-out dependency structure.
		public CredentialEdgeSet DrainTerminal(RequestNode node, IEnumerable<RequestNode> nodes)
			=> nodes.Aggregate(this, (edgeSet, otherNode) => edgeSet.DrainTerminal(node, otherNode));

		private CredentialEdgeSet DrainTerminal(RequestNode node, RequestNode dischargeNode)
		{
			long value = Balance(dischargeNode);

			if (value > 0)
			{
				return AddEdge(dischargeNode, node, (ulong)Math.Min(-1 * Balance(node), value));
			}
			else if (value < 0)
			{
				return AddEdge(node, dischargeNode, (ulong)Math.Min(Balance(node), -1 * value));
			}
			else
			{
				throw new InvalidOperationException("Can't drain terminal nodes with 0 balance");
			}
		}
	}
}

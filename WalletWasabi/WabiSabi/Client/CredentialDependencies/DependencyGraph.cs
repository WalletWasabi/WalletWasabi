using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	public class DependencyGraph
	{
		public const int K = ProtocolConstants.CredentialNumber;

		private DependencyGraph(IEnumerable<IEnumerable<long>> initialValues)
		{
			if (initialValues.Any(x => x.Count() != (int)CredentialType.NumTypes))
			{
				throw new ArgumentException($"Number of credential values must be {CredentialType.NumTypes}");
			}
			if (initialValues.Any(x => x.Where(y => y != 0).Select(y => y.CompareTo(0)).Distinct().Count() != 1))
			{
				throw new ArgumentException($"Credential values of a node must all have the same sign.");
			}

			for (CredentialType i = 0; i < CredentialType.NumTypes; i++)
			{
				if (initialValues.Sum(x => x.Skip((int)i).First()) < 0)
				{
					throw new ArgumentException("Overall balance must not be negative");
				}
			}

			// per node entries created in AddNode, querying nodes not in the
			// graph should result in key errors.

			Successors = Enumerable.Repeat(0, (int)CredentialType.NumTypes).Select(_ => new Dictionary<int, HashSet<CredentialDependency>>()).ToImmutableArray();
			Predecessors = Enumerable.Repeat(0, (int)CredentialType.NumTypes).Select(_ => new Dictionary<int, HashSet<CredentialDependency>>()).ToImmutableArray();
			EdgeBalances = Enumerable.Repeat(0, (int)CredentialType.NumTypes).Select(_ => new Dictionary<int, long>()).ToImmutableArray();

			Vertices = ImmutableList<RequestNode>.Empty;

			foreach (var node in initialValues.Select((values, i) => new RequestNode(i, values.ToImmutableArray())))
			{
				// enforce at least one value != 0? all values? it doesn't actually matter for the algorithm
				AddNode(node);
			}
		}

		public ImmutableList<RequestNode> Vertices { get; private set; }

		// Internal properties used to keep track of effective values and edges
		private ImmutableArray<Dictionary<int, long>> EdgeBalances { get; }
		private ImmutableArray<Dictionary<int, HashSet<CredentialDependency>>> Successors { get; }
		private ImmutableArray<Dictionary<int, HashSet<CredentialDependency>>> Predecessors { get; }

		// TODO doc comment
		// Public API: construct a graph from amounts, and resolve the
		// credential dependencies. Should only produce valid graphs.
		// IDs are positive ints assigned in the order of the enumerable, but
		// Vertices will contain more elements if there are reissuance nodes.
		public static DependencyGraph ResolveCredentialDependencies(IEnumerable<IEnumerable<long>> amounts)
		{
			var graph = new DependencyGraph(amounts);
			graph.ResolveCredentials();
			return graph;
		}

		public IOrderedEnumerable<RequestNode> VerticesByBalance(CredentialType type) => Vertices.OrderByDescending(x => Balance(x, type));

		public IEnumerable<CredentialDependency> InEdges(RequestNode node, CredentialType credentialType) =>
			Predecessors[(int)credentialType][node.Id].ToImmutableArray();

		public IEnumerable<CredentialDependency> OutEdges(RequestNode node, CredentialType credentialType) =>
			Successors[(int)credentialType][node.Id].ToImmutableArray();

		private void AddNode(RequestNode node)
		{
			for (CredentialType credentialType = 0; credentialType < CredentialType.NumTypes; credentialType++)
			{
				var balances = EdgeBalances[(int)credentialType];
				if (balances.ContainsKey(node.Id))
				{
					throw new InvalidOperationException($"Node {node.Id} already exists in graph");
				}
				else
				{
					balances[node.Id] = 0;
				}

				foreach (var container in new[] { Successors[(int)credentialType], Predecessors[(int)credentialType] })
				{
					if (container.ContainsKey(node.Id))
					{
						throw new InvalidOperationException($"Node {node.Id} already exists in graph");
					}
					else
					{
						container[node.Id] = new HashSet<CredentialDependency>();
					}
				}
			}

			Vertices = Vertices.Add(node);
		}

		public long Balance(RequestNode node, CredentialType type) =>
			node.InitialBalance(type) + EdgeBalances[(int)type][node.Id];

		public int InDegree(RequestNode node, CredentialType credentialType) =>
			Predecessors[(int)credentialType][node.Id].Count;

		public int OutDegree(RequestNode node, CredentialType credentialType) =>
			Successors[(int)credentialType][node.Id].Count;

		private void AddEdge(CredentialDependency edge)
		{
			var successors = Successors[(int)edge.CredentialType][edge.From.Id];
			var predecessors = Predecessors[(int)edge.CredentialType][edge.To.Id];

			// Maintain subset of K-regular graph invariant
			if (successors.Count == K || predecessors.Count == K)
			{
				throw new InvalidOperationException("Can't add more than k edges");
			}

			if (predecessors.Count == K-1)
			{
				// This is the final in edge edge for the node edge.To
				if ( Balance(edge.To, edge.CredentialType) + (long)edge.Value < 0 )
				{
					throw new InvalidOperationException("Can't add final edge without discharging negative value");
				}

				// If it's the final edge overall, the final balance must be 0
				if ( OutDegree(edge.To, edge.CredentialType) == K && Balance(edge.To, edge.CredentialType) + (long)edge.Value != 0 )
				{
					throw new InvalidOperationException("Can't add final edge without discharging negative value");
				}
			}

			// The edge sum invariant are only checked after the graph is
			// completed, it's too early to enforce that here without context
			// (ensuring that if all K
			EdgeBalances[(int)edge.CredentialType][edge.From.Id] -= (long)edge.Value;
			successors.Add(edge);

			EdgeBalances[(int)edge.CredentialType][edge.To.Id] += (long)edge.Value;
			predecessors.Add(edge);
		}

		private void ResolveCredentials()
		{
			for (CredentialType credentialType = 0; credentialType < CredentialType.NumTypes; credentialType++)
			{
				// Stop when no negative valued nodes remain. The total sum is
				// positive, so by discharging elements of opposite values this
				// list is guaranteed to be reducible until empty.
				while (SelectNodesToDischarge(credentialType, out RequestNode? largestMagnitudeNode, out IEnumerable<RequestNode>? smallMagnitudeNodes))
				{
					var largestIsSink = largestMagnitudeNode!.InitialBalance(credentialType).CompareTo(smallMagnitudeNodes!.First().InitialBalance(credentialType)) == -1;
					var maxCount = K - (largestIsSink ? InDegree(largestMagnitudeNode, credentialType) : OutDegree(largestMagnitudeNode, credentialType));

					if (Math.Abs(Balance(largestMagnitudeNode, credentialType)) > Math.Abs(smallMagnitudeNodes!.Sum(x => Balance(x, credentialType))))
					{
						// When we are draining a positive valued node into
						// multiple negative nodes and we can't drain it
						// completely, we need to leave an edge unused for the
						// remaining amount.
						// The corresponding condition can't actually happen for fan-in
						// because the negative balance of the last loop
						// iteration can't exceed the the remaining positive
						// elements, their total sum must be positive as checked
						// in the constructor.
						maxCount--;
					}

					// Reduce the number of small magnitude nodes to the number
					// of edges available for use in the largest magnitude node
					smallMagnitudeNodes = LimitCount(smallMagnitudeNodes!, maxCount, credentialType);

					// After draining either the last small magnitude node or
					// the largest magnitude node could still have a non-zero
					// value.
					DrainTerminal(largestMagnitudeNode!, smallMagnitudeNodes!, credentialType);
				}

				// at this point the sub-graph of credentialType edges should be
				// a planar DAG with the AssertResolvedGraphInvariants() holding for
				// that particular type.
			}

			// at this point the entire graoh should be a DAG with labeled
			// edges that can be partitioned into NumTypes different planar
			// DAGs, and the invariants should hold for all of these.
		}

		// Find the largest negative or positive balance node for the given
		// credential type, and one or more smaller nodes with a combined total
		// magnitude exceeding that of the largest magnitude node when possible.
		public bool SelectNodesToDischarge(CredentialType credentialType, out RequestNode? largestMagnitudeNode, out IEnumerable<RequestNode>? smallMagnitudeNodes)
		{
			// Order the nodes of the graph based on their balances
			var ordered = VerticesByBalance(credentialType);
			ImmutableArray<RequestNode> positive = ordered.ThenBy(x => OutDegree(x, credentialType)).Where(v => Balance(v, credentialType) > 0).ToImmutableArray();
			ImmutableArray<RequestNode> negative = Enumerable.Reverse(ordered.ThenByDescending(x => InDegree(x, credentialType)).Where(v => Balance(v, credentialType) < 0)).ToImmutableArray();

			if (negative.Length == 0)
			{
				largestMagnitudeNode = null;
				smallMagnitudeNodes = null;
				return false;
			}

			var nPositive = 1;
			var nNegative = 1;

			IEnumerable<RequestNode> posCandidates() => positive.Take(nPositive);
			IEnumerable<RequestNode> negCandidates() => negative.Take(nNegative);
			long posSum() => posCandidates().Sum(x => Balance(x, credentialType));
			long negSum() => negCandidates().Sum(x => Balance(x, credentialType));
			long compare() => posSum().CompareTo(-1 * negSum());

			// We want to fully discharge the larger (in absolute magnitude) of
			// the two nodes, so we will add more nodes to the smaller one until
			// we can fully cover. At each step of the iteration we fully
			// discharge at least 2 nodes from the queue.
			var initialComparison = compare();
			var fanIn = initialComparison == -1;

			if (initialComparison != 0)
			{
				Action takeOneMore = fanIn ? () => nPositive++ : () => nNegative++;

				// Take more nodes until the comparison sign changes or
				// we run out.
				while (initialComparison == compare()
				       && (fanIn ? positive.Length - nPositive > 0
				                 : negative.Length - nNegative > 0))
				{
					takeOneMore();
				}
			}

			largestMagnitudeNode = (fanIn ? negative.First() : positive.First() );
			smallMagnitudeNodes = (fanIn ? positive.Take(nPositive).Reverse() : negative.Take(nNegative)); // reverse positive values so we always proceed in order of increasing magnitude

			return true;
		}

		// Build a k-ary tree bottom up to reduce a list of nodes.
		private ImmutableList<RequestNode> LimitCount(IEnumerable<RequestNode> nodes, int maxCount, CredentialType credentialType)
		{
			var nodeQueue = nodes.ToImmutableList();

			while (nodeQueue.Count > maxCount)
			{
				// Replace up to k nodes, possibly the entire queue, with a
				// single reissuance node which combines their values. The total
				// number of items might be less than K but still larger than
				// maxCount.
				var take = Math.Min(K, nodeQueue.Count);
				var reissuance = NewReissuanceNode();
				DrainReissuance(reissuance, nodeQueue.Take(take), credentialType);
				nodeQueue = nodeQueue.RemoveRange(0, take).Add(reissuance);
			}

			return nodeQueue;
		}

		private RequestNode NewReissuanceNode()
		{
			var node = new RequestNode(Vertices.Count, Enumerable.Repeat(0L, K).ToImmutableArray());
			AddNode(node);
			return node;
		}

		// Drain values into a reissuance request (towards the center of the graph).
		private void DrainReissuance(RequestNode x, IEnumerable<RequestNode> ys, CredentialType type)
		{
			// The nodes' initial balance determines edge direction. Reissuance
			// nodes always have a 0 initial value.
			var xIsSink = x.InitialBalance(type).CompareTo(ys.First().InitialBalance(type)) == -1;

			foreach (var y in ys)
			{
				// The amount for the edge is always determined by the `y`
				// values, since we only add reissuance nodes to reduce the
				// number of values required.
				long amount = Balance(y, type);

				if (xIsSink)
				{
					AddEdge(new(y, x, type, (ulong)amount));
				}
				else
				{
					AddEdge(new(x, y, type, (ulong)(-1 * amount)));
				}

				// Also drain all of the other credential types, to minimize
				// dependencies between different requests, weight
				// credentials should often be easily satisfiable with
				// parallel edges to the amount credential edges.
				for (CredentialType extraType = type + 1; extraType < CredentialType.NumTypes; extraType++)
				{
					var extraBalance = Balance(y, extraType);
					if (extraBalance != 0 )
					{
						if (xIsSink)
						{
							AddEdge(new(y, x, extraType, (ulong)extraBalance));
						}
						else
						{
							AddEdge(new(x, y, extraType, (ulong)(-1 * extraBalance)));
						}
					}
				}
			}
		}

		// Drain credential values between terminal nodes, cancelling out
		// opposite values by propagating forwards or backwards corresponding to
		// fan-in and fan-out dependency structure.
		private void DrainTerminal(RequestNode x, IEnumerable<RequestNode> ys, CredentialType type)
		{
			// The nodes' initial balance determines edge direction.
			var xIsSink = x.InitialBalance(type).CompareTo(ys.First().InitialBalance(type)) == -1;

			foreach (var y in ys)
			{
				long amount = Balance(y, type);

				if (xIsSink)
				{
					AddEdge(new(y, x, type, (ulong)amount));
				}
				else
				{
					// When x is the source, we can only utilize its remaining
					// balance. The effective balance of the last y term term
					// might still have a negative magnitude after this.
					AddEdge(new(x, y, type, (ulong)Math.Min(Balance(x, type), -1 * amount)));
				}

				// Here we avoid opportunistically adding edges as it provides no
				// benefit with K=2. Stable sorting prevents edge crossing.
			}
		}
	}
}

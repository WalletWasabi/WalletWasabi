using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	public record DependencyGraph
	{
		public const int K = ProtocolConstants.CredentialNumber;

		public ImmutableList<RequestNode> Vertices { get; private set; } = ImmutableList<RequestNode>.Empty;

		// Internal properties used to keep track of effective values and edges
		private ImmutableDictionary<CredentialType, CredentialEdgeSet> edgeSets { get; init; }
			= ImmutableDictionary<CredentialType, CredentialEdgeSet>.Empty
			.Add(CredentialType.Amount, new() { CredentialType = CredentialType.Amount })
			.Add(CredentialType.VirtualBytes, new() { CredentialType = CredentialType.VirtualBytes });

		public long Balance(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].Balance(node);

		public IEnumerable<CredentialDependency> InEdges(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].InEdges(node);

		public IEnumerable<CredentialDependency> OutEdges(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].OutEdges(node);

		public int InDegree(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].InDegree(node);

		public int OutDegree(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].OutDegree(node);

		// TODO doc comment
		// Public API: construct a graph from amounts, and resolve the
		// credential dependencies. Should only produce valid graphs.
		// IDs are positive ints assigned in the order of the enumerable, but
		// Vertices will contain more elements if there are reissuance nodes.
		public static DependencyGraph ResolveCredentialDependencies(IEnumerable<IEnumerable<ulong>> inputValues, IEnumerable<IEnumerable<ulong>> outputValues)
		{
			var combinedValues = inputValues.Select(x => x.Select(y => (long)y)).Concat(outputValues.Select(x => x.Select(y => -1 * (long)y)));
			return FromValues(combinedValues).ResolveCredentials();
		}

		private static DependencyGraph FromValues(IEnumerable<IEnumerable<long>> initialValues)
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
			return new DependencyGraph().AddNodes(initialValues.Select(v => new RequestNode(v.ToImmutableArray())));
		}

		private DependencyGraph AddNodes(IEnumerable<RequestNode> nodes) => nodes.Aggregate(this, (g, v) => g.AddNode(v));

		private DependencyGraph AddNode(RequestNode node)
			=> this with
			{
				Vertices = Vertices.Add(node),
				edgeSets = edgeSets.ToImmutableDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value with
					{
						EdgeBalances = kvp.Value.EdgeBalances.Add(node, 0),
						Predecessors = kvp.Value.Predecessors.Add(node, ImmutableHashSet<CredentialDependency>.Empty),
						Successors = kvp.Value.Successors.Add(node, ImmutableHashSet<CredentialDependency>.Empty)
					}
				),
			};

		private DependencyGraph ResolveCredentials()
			=> edgeSets.Keys.Aggregate(this, (g, credentialType) => g.ResolveCredentials(credentialType));

		private DependencyGraph ResolveCredentials(CredentialType credentialType)
		{
			// Stop when no negative valued nodes remain. The total sum is
			// positive, so by discharging elements of opposite values this
			// list is guaranteed to be reducible until empty.
			if (!edgeSets[credentialType].SelectNodesToDischarge(Vertices, out RequestNode? largestMagnitudeNode, out IEnumerable<RequestNode> smallMagnitudeNodes, out bool fanIn))
			{
				return this;
			}

			var edgeSet = edgeSets[credentialType];
			var maxCount = (fanIn ? edgeSet.RemainingInDegree(largestMagnitudeNode!) : edgeSet.RemainingOutDegree(largestMagnitudeNode!));

			var g = this;

			if (Math.Abs(edgeSet.Balance(largestMagnitudeNode!)) > Math.Abs(smallMagnitudeNodes.Sum(x => edgeSet.Balance(x))))
			{
				// When we are draining a positive valued node into multiple
				// negative nodes and we can't drain it completely, we need to
				// leave an edge unused for the remaining amount.
				// The corresponding condition can't actually happen for fan-in
				// because the negative balance of the last loop iteration can't
				// exceed the the remaining positive elements, their total sum
				// must be positive as checked in the constructor.
				if (maxCount > 1)
				{
					// when the edge capacity makes it possible, we can just
					// ensure the largest magnitude node ends up with an unused
					// edge by reducing maxCount
					maxCount--;
				}
				else
				{
					// otherwise, drain the largest magnitudenode into a new
					// reissuance node which will have room for an unused edge
					// in its out edge set.
					(g, largestMagnitudeNode) = AggregateIntoReissuanceNode(new RequestNode[] { largestMagnitudeNode! }, credentialType);
				}
			}

			// Reduce the number of small magnitude nodes to the number of edges
			// available for use in the largest magnitude node
			(g, smallMagnitudeNodes) = g.ReduceNodes(smallMagnitudeNodes, maxCount, credentialType);

			// After draining either the last small magnitude node or the
			// largest magnitude node could still have a non-zero value.
			return g.DrainTerminal(largestMagnitudeNode!, smallMagnitudeNodes, credentialType).ResolveCredentials(credentialType);
		}

		// Build a k-ary tree bottom up to reduce a list of nodes to discharge
		// to at most maxCount elements.
		private (DependencyGraph, IEnumerable<RequestNode>) ReduceNodes(IEnumerable<RequestNode> nodes, int maxCount, CredentialType credentialType)
		{
			if (nodes.Count() <= maxCount)
			{
				return (this, nodes);
			}

			// Replace up to k nodes, possibly the entire queue, with a
			// single reissuance node which combines their values. The total
			// number of items might be less than K but still larger than
			// maxCount.
			var take = Math.Min(K, nodes.Count());
			(var g, var reissuance) = AggregateIntoReissuanceNode(nodes.Take(take), credentialType);
			var reduced = nodes.Skip(take).Append(reissuance).ToImmutableArray(); // keep enumerable expr size bounded by evaluating eagerly
			return g.ReduceNodes(reduced, maxCount, credentialType);
		}

		private (DependencyGraph, RequestNode) AggregateIntoReissuanceNode(IEnumerable<RequestNode> nodes, CredentialType credentialType)
		{
			var reissuance = new RequestNode(Enumerable.Repeat(0L, K).ToImmutableArray());
			return (AddNode(reissuance).DrainReissuance(reissuance, nodes, credentialType), reissuance);
		}

		private DependencyGraph DrainReissuance(RequestNode reissuance, IEnumerable<RequestNode> nodes, CredentialType credentialType)
		{
			var drainedEdgeSet = edgeSets[credentialType].DrainReissuance(reissuance, nodes);
			var g = this with { edgeSets = edgeSets.SetItem(credentialType, drainedEdgeSet) };

			// Also drain all subsequent credential types, to minimize
			// dependencies between different requests, weight credentials
			// should often be easily satisfiable with parallel edges to the
			// amount credential edges.
			if (credentialType + 1 < CredentialType.NumTypes)
			{
				return g.DrainReissuance(reissuance, nodes, credentialType + 1);
			}
			else
			{
				return g;
			}
		}

		private DependencyGraph DrainTerminal(RequestNode node, IEnumerable<RequestNode> nodes, CredentialType credentialType)
			// Here we avoid opportunistically adding edges of other types as it
			// provides no benefit with K=2. Stable sorting prevents edge
			// crossing.
			=> this with { edgeSets = edgeSets.SetItem(credentialType, edgeSets[credentialType].DrainTerminal(node, nodes)) };
	}
}

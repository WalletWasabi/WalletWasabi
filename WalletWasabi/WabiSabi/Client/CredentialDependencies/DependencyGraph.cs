using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace WalletWasabi.WabiSabi.Client.CredentialDependencies
{
	public record DependencyGraph
	{
		public const int K = ProtocolConstants.CredentialNumber;

		public static ImmutableArray<CredentialType> CredentialTypes { get; } = ImmutableArray.Create<CredentialType>(CredentialType.Amount, CredentialType.Vsize);

		public ImmutableList<RequestNode> Vertices { get; private set; } = ImmutableList<RequestNode>.Empty;

		public ImmutableList<RequestNode> Inputs { get; private set; } = ImmutableList<RequestNode>.Empty;

		public ImmutableList<RequestNode> Outputs { get; private set; } = ImmutableList<RequestNode>.Empty;

		public ImmutableList<RequestNode> Reissuances { get; private set; } = ImmutableList<RequestNode>.Empty; // TODO sort

		// Internal properties used to keep track of effective values and edges
		public ImmutableSortedDictionary<CredentialType, CredentialEdgeSet> edgeSets { get; init; } = ImmutableSortedDictionary<CredentialType, CredentialEdgeSet>.Empty
			.Add(CredentialType.Amount, new() { CredentialType = CredentialType.Amount })
			.Add(CredentialType.Vsize, new() { CredentialType = CredentialType.Vsize });

		public long Balance(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].Balance(node);

		public IEnumerable<CredentialDependency> InEdges(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].InEdges(node);

		public IEnumerable<CredentialDependency> OutEdges(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].OutEdges(node);

		public int InDegree(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].InDegree(node);

		public int OutDegree(RequestNode node, CredentialType credentialType) => edgeSets[credentialType].OutDegree(node);

		/// <summary>Construct a graph from amounts, and resolve the
		/// credential dependencies.</summary>
		///
		/// <remarks>Should only produce valid graphs. The elements of the
		/// <see>Vertices</see> property will correspond to the given values in order,
		/// and may contain additional nodes if reissuance requests are
		/// required.</remarks>
		public static DependencyGraph ResolveCredentialDependencies(IEnumerable<IEnumerable<ulong>> inputValues, IEnumerable<IEnumerable<ulong>> outputValues)
		{
			return FromValues(inputValues, outputValues).ResolveCredentials();
		}

		public static DependencyGraph FromValues(IEnumerable<IEnumerable<ulong>> inputValues, IEnumerable<IEnumerable<ulong>> outputValues)
		{
			if (Enumerable.Concat(inputValues, outputValues).Any(x => x.Count() != CredentialTypes.Length))
			{
				throw new ArgumentException($"Number of credential values must be {CredentialTypes.Length}");
			}

			foreach (var credentialType in CredentialTypes)
			{
				// no Sum(Func<ulong, ulong>)) variant
				long credentialValue(IEnumerable<ulong> x) => (long)x.Skip((int)credentialType).First();

				if (inputValues.Sum(credentialValue) < outputValues.Sum(credentialValue))
				{
					throw new ArgumentException("Overall balance must not be negative.");
				}
			}

			return new DependencyGraph().AddInputs(inputValues).AddOutputs(outputValues);
		}

		public DependencyGraph AddNode(RequestNode node)
			=> this with
			{
				Vertices = Vertices.Add(node),
				edgeSets = edgeSets.ToImmutableSortedDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value with
					{
						EdgeBalances = kvp.Value.EdgeBalances.Add(node, 0),
						Predecessors = kvp.Value.Predecessors.Add(node, ImmutableHashSet<CredentialDependency>.Empty),
						Successors = kvp.Value.Successors.Add(node, ImmutableHashSet<CredentialDependency>.Empty)
					}
				),
			};

		// Input nodes represent a combination of an input registration and
		// connection confirmation. Connection confirmation requests actually
		// have indegree K, not 0, which could be used to consolidate inputs
		// early but using it implies connection confirmations may have
		// dependencies, posing some complexity for a privacy preserving
		// approach.
		private DependencyGraph AddInput(IEnumerable<ulong> values)
		{
			var node = new RequestNode(values.Select(y => (long)y), inDegree: 0, outDegree: K, zeroOnlyOutDegree: K * (K - 1));
			return (this with { Inputs = Inputs.Add(node) }).AddNode(node);
		}

		private DependencyGraph AddOutput(IEnumerable<ulong> values)
		{
			var node = new RequestNode(values.Select(y => -1 * (long)y), inDegree: K, outDegree: 0, zeroOnlyOutDegree: 0);
			return (this with { Outputs = Outputs.Add(node) }).AddNode(node);
		}
		private DependencyGraph AddInputs(IEnumerable<IEnumerable<ulong>> values) => values.Aggregate(this, (g, v) => g.AddInput(v));

		private DependencyGraph AddOutputs(IEnumerable<IEnumerable<ulong>> values) => values.Aggregate(this, (g, v) => g.AddOutput(v));

		private (DependencyGraph, RequestNode) AddReissuance()
		{
			// TODO insert into reissuance requests property
			var node = new RequestNode(Enumerable.Repeat(0L, K), inDegree: K, outDegree: K, zeroOnlyOutDegree: K * (K - 1));
			return ((this with { Reissuances = Reissuances.Add(node) }).AddNode(node), node); // TODO keep sorted by topological order?
		}

		/// <summary>Resolve edges for all credential types</summary>
		///
		/// <remarks><para>We start with a bipartite graph of terminal sources
		/// and sinks (corresponding to inputs and outputs or connection
		/// confirmation and output registration requests).</para>
		///
		/// <para>Nodes are fully discharged when all of their in-edges are
		/// accounted for. For output registrations this must exactly cancel out
		/// their initial balance, since they make no output registration
		/// requests.</para>
		///
		/// <para>Outgoing edges represent credential amounts to request and
		/// present in a subsequent request, so for positive nodes if there is a
		/// left over balance the outgoing dregree is limited to K-1, since an
		/// extra credential for the remaining amount must also be
		/// requested.</para>
		///
		/// <para>At every iteration of the loop a single node of the largest
		/// magnitude (source or sink) and one or more nodes of opposite sign
		/// are selected. Unless these are the final nodes on the list, the
		/// smaller magnitude nodes are selected to fully discharge the largest
		/// magnitude node.</para>
		///
		/// <para>If the smaller nodes are too numerous K at a time are merged
		/// into a reissuance node. When these are output registrations the
		/// reissuance node's output edges always fully account for the
		/// dependent requests, including the zero credentials required for
		/// them. When the smaller nodes appear on the input side, the non-zero
		/// values are sufficient to fill the reissuance node's in edge set and
		/// requires only one edge to fully drain the (remaining) balance, so
		/// there will be an extra zero valued credential (requested normally,
		/// incl. range proof).</para>
		///
		/// <para>New reissuance nodes fully absorb the value of the nodes they
		/// substitute with no additional dependencies required, so each one
		/// reduces the bipartite graph problem to a smaller one (by K-1 == 1),
		/// since the replaced nodes no longer need to be considered.</para>
		///
		/// <para>When the list of nodes has been reduced to the remaining
		/// non-zero out degree of the largest magnitude node edges that cancel
		/// out positive and negative values are added. This will fully
		/// discharge the largest magnitude node, except when it is positive and
		/// all of the remaining negative nodes on the graph add up to less than
		/// it.</para>
		///
		/// <para>There are two special related special cases affecting vbyte
		/// credentials - when the positive valued nodes are uniform and
		/// sufficient for all of the negative amounts with leftovers, instead
		/// of taking the largest node all of the negative nodes are reduced
		/// together resulting in a more balanced structure overall. This is
		/// combined with another special case that checks if each of the
		/// positive valued nodes are all strictly greater than a smaller number
		/// of negative nodes (uniform values are a special case of this), the
		/// positive and negative nodes will be zipped with each node only
		/// requiring a single edge. These can optimize amount credentials as
		/// well when consolidating multiple equal valued inputs, but is only
		/// expected to regularly occur for vbyte credentials.</para>
		///
		/// <para>After all negative value nodes have been discharged, the
		/// remaining in-edges of all nodes must be filled with zero
		/// credentials. These are added according to the graph order, by
		/// extending new edges from nodes whose in-degree is already maximized
		/// but whose out degree is not. This again deals only with a bipartite
		/// graph, because reissuance nodes consolidating output nodes leave no
		/// zero edges unaccounted for in the nodes they replace, whereas on the
		/// input side the structure fans in so necessarily it leaves no nodes
		/// with non-maximized in-degrees, it can only increase the available
		/// out degree for nodes which are not fully consumed.</para></remarks>
		private DependencyGraph ResolveCredentials()
			=> CredentialTypes.Aggregate(
				CredentialTypes.Aggregate(this, (g, credentialType) => g.ResolveNegativeBalanceNodes(credentialType)),
				(g, credentialType) => g.ResolveZeroCredentials(credentialType));

		private DependencyGraph ResolveNegativeBalanceNodes(CredentialType credentialType)
		{
			var g = ResolveUniformInputSpecialCases(credentialType);

			var edgeSet = g.edgeSets[credentialType];

			var negative = g.Vertices.Where(v => edgeSet.Balance(v) < 0);

			if (!negative.Any())
			{
				return g;
			}

			var positive = g.Vertices.Where(v => edgeSet.Balance(v) > 0);

			(var largestMagnitudeNode, var smallMagnitudeNodes, var fanIn) = edgeSet.MatchNodesToDischarge(positive, negative);

			var maxCount = (fanIn ? edgeSet.RemainingInDegree(largestMagnitudeNode) : edgeSet.RemainingOutDegree(largestMagnitudeNode));

			if (Math.Abs(edgeSet.Balance(largestMagnitudeNode)) > Math.Abs(smallMagnitudeNodes.Sum(x => edgeSet.Balance(x))))
			{
				// When we are draining a positive valued node into multiple
				// negative nodes and we can't drain it completely, we need to
				// leave an edge unused for the remaining amount.
				// The corresponding condition can't actually happen for fan-in
				// because the negative balance of the last loop iteration can't
				// exceed the the remaining positive elements, their total sum
				// must be positive as checked in the constructor.
				// maxCount-- could make sense here if it's >= 2, but that's
				// already handled by ResolveUniformInputSpecialCases. Just
				// Drain the largest magnitude node into a new reissuance node
				// which will have room for an unused edge in its out edge set.
				(g, largestMagnitudeNode) = g.AggregateIntoReissuanceNode(new RequestNode[] { largestMagnitudeNode }, credentialType);
			}

			var preReduceSmallNodes = smallMagnitudeNodes;

			// Reduce the number of small magnitude nodes to the number of edges
			// available for use in the largest magnitude node
			(g, smallMagnitudeNodes) = g.ReduceNodes(smallMagnitudeNodes, maxCount, credentialType);

			// After draining either the last small magnitude node or the
			// largest magnitude node could still have a non-zero value.
			g = g.DrainTerminal(largestMagnitudeNode, smallMagnitudeNodes, credentialType);

			return g.ResolveNegativeBalanceNodes(credentialType);
		}

		private DependencyGraph ResolveUniformInputSpecialCases(CredentialType credentialType)
		{
			var edgeSet = edgeSets[credentialType];

			IEnumerable<RequestNode> negative = Vertices.Where(v => edgeSet.Balance(v) < 0).OrderBy(v => edgeSet.Balance(v));

			if (!negative.Any())
			{
				return this;
			}

			var g = this;

			// First special case, handle uniform input values (should be the
			// case for vbyte credentials) more efficiently, by spreading the
			// negative nodes evenly.

			// Unconstrained nodes have a remaining out degree greater than 1,
			// so they can produce arbitary value outputs (final edge must leave
			// balance = 0).
			// The remaining outdegree > 1 condition is equivalent to == K for
			// K=2, so that also implies the positive valued nodes haven't been
			// used for this credential type yet (TODO affinity/avoid crossing?).
			var unconstrainedPositive = Vertices.Where(v => edgeSet.Balance(v) > 0 && edgeSet.RemainingOutDegree(v) > 1).OrderByDescending(v => edgeSet.Balance(v));
			if (unconstrainedPositive.Select(v => edgeSet.Balance(v)).Distinct().Count() == 1)
			{
				if (edgeSet.Balance(negative.First()) * -1 * (negative.Count() / unconstrainedPositive.Count()) < edgeSet.Balance(unconstrainedPositive.First()))
				{
					// TODO shuffle? not clear if it will benefit or reduce
					// privacy, need to study it in more detail. The adverserial
					// setting that should be considered is one where the
					// coordinator can delay information and request processing
					// and perform an arbitrary number of blameless round
					// failures while attempting to detect correlations between
					// the timings of the registrations of a single user. Random
					// delays should add noise to this and ideally all clients
					// should aim to have the same fixed baseline delay for
					// their output registrations (something that should almost
					// always be a few times longer than the expected RTT), then
					// output registrations should mostly follow reissuances and
					// be difficult to tell apart based on timing, which should
					// render the ordering irrelevant.

					if (negative.Count() > unconstrainedPositive.Count())
					{
						// TODO consolidate only lowest depth at each iteration
						// (negative height, i.e. max of distance to terminal output
						// nodes derived from the vertex, 0 for such nodes) nodes
						// first. By only consolidating nodes with the lowest
						// depth a balanced structure will be ensured regardless
						// of prior reissuances potentially creating deeper
						// nodes.
						(g, negative) = g.ReduceNodes(negative, unconstrainedPositive.Count(), credentialType);
					}
				}
			}

			edgeSet = g.edgeSets[credentialType];

			// Second special case, more general to the previous one, when for each
			// negative node there is a satisfactory unconstrained positive node
			// (not necessarily all of equal value), discharge via a 1:1 correspondence
			if (negative.Count() <= unconstrainedPositive.Count() && Enumerable.Zip(unconstrainedPositive, negative).All(p => edgeSet.Balance(p.First) + edgeSet.Balance(p.Second) >= 0))
			{
				g = Enumerable.Zip(unconstrainedPositive, negative).Aggregate(g, (g, p) => g.DrainTerminal(p.First, new RequestNode[] { p.Second }, credentialType));
			}

			return g;
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
			(var g, var reissuance) = AddReissuance();

			g = g.DrainReissuance(reissuance, nodes, credentialType);

			// This is kind of a hack, also discharge 0 credentials for *previous*
			// credential type from this reissuance node, which will eliminate
			// it from the subsequent zero credential filling passes.
			// The rationale behind this is that the reissuance node already has
			// to be created and will have zero credentials to spare, so in this
			// way the aggregated nodes are not dependent on any other node for
			// zero credentials.
			if (credentialType > 0)
			{
				g = nodes.Aggregate(g, (g, v) => g.DrainZeroCredentials(reissuance, v, 0));
			}

			return (g, reissuance);
		}

		private DependencyGraph DrainReissuance(RequestNode reissuance, IEnumerable<RequestNode> nodes, CredentialType credentialType)
		{
			var drainedEdgeSet = edgeSets[credentialType].DrainReissuance(reissuance, nodes);

			var g = this with { edgeSets = edgeSets.SetItem(credentialType, drainedEdgeSet) };

			// Also drain all subsequent credential types, to minimize
			// dependencies between different requests, weight credentials
			// should often be easily satisfiable with parallel edges to the
			// amount credential edges.
			if (CredentialTypes.Contains(credentialType + 1))
			{
				// TODO Limit up to a certain height in the graph, no more than
				// the initial value, this can sometimes create a deeper graph
				// than necessary by being too greedy about consolidating vbyte
				// nodes and making them larger than the per input vbyte
				// allocation. Should be rare in practice though, so ignored for
				// now.
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

		private DependencyGraph ResolveZeroCredentials(CredentialType credentialType)
		{
			// TODO improve affinity/reduce crossings:
			// resolve 0 credentials in 3 depth first passes, always discharging
			// from remaining in = 0, available zero > 1 nodes.
			// each pass should be iterated several times until reaching a fixed
			// point (no more progress).
			// - discharge only to AvailableZeroOutDegree > 1 nodes (should be 1 per)
			// - discharge to direct descendents even if a net reduction in zero creds
			// - discharge all remaining in degree >1 nodes by topological order

			var edgeSet = edgeSets[credentialType];
			var unresolvedNodes = Vertices.Where(v => edgeSet.RemainingInDegree(v) > 0 && edgeSet.AvailableZeroOutDegree(v) > 0).OrderByDescending(v => edgeSet.AvailableZeroOutDegree(v));

			if (!unresolvedNodes.Any())
			{
				return ResolveZeroCredentialsForTerminalNodes(credentialType); ;
			}

			// Resolve remaining zero credentials by using nodes with no
			// dependencies but remaining out degree (following DAG order)
			var providers = Vertices.Where(v => edgeSet.RemainingInDegree(v) == 0 && edgeSet.AvailableZeroOutDegree(v) > 0)
				.SelectMany(v => Enumerable.Repeat(v, edgeSet.AvailableZeroOutDegree(v)));

			// TODO discharge iteratively by topological layer, each time applying different filters?
			// intersect with successor set of providers of lower credential type edge set for better affinity?

			var reduced = unresolvedNodes.SelectMany(v => Enumerable.Repeat(v, edgeSet.RemainingInDegree(v)))
				.Zip(providers, (t, f) => new { From = f, To = t })
				.Aggregate(this, (g, p) => g.AddZeroCredential(p.From, p.To, credentialType));

			return reduced.ResolveZeroCredentials(credentialType);
		}

		// Final pass, ensure that no RemainingInDegree = 0 nodes remain
		// TODO remove code duplication
		private DependencyGraph ResolveZeroCredentialsForTerminalNodes(CredentialType credentialType)
		{
			// Stop when all nodes have a maxed out in-degree.
			// This termination condition is guaranteed to be possible because
			// connection confirmation and reissuance requests both have an out
			// degree of K^2 when accounting for their extra zero credentials.
			var edgeSet = edgeSets[credentialType];
			var unresolvedNodes = Vertices.Where(v => edgeSet.RemainingInDegree(v) > 0).OrderByDescending(v => edgeSet.AvailableZeroOutDegree(v));

			if (!unresolvedNodes.Any())
			{
				return this;
			}

			// Resolve remaining zero credentials by using nodes with no
			// dependencies but remaining out degree (following DAG order)
			var providers = Vertices.Where(v => edgeSet.RemainingInDegree(v) == 0 && edgeSet.AvailableZeroOutDegree(v) > 0)
				.SelectMany(v => Enumerable.Repeat(v, edgeSet.AvailableZeroOutDegree(v)));

			var reduced = unresolvedNodes.SelectMany(v => Enumerable.Repeat(v, edgeSet.RemainingInDegree(v)))
				.Zip(providers, (t, f) => new { From = f, To = t })
				.Aggregate(this, (g, p) => g.AddZeroCredential(p.From, p.To, credentialType));

			return reduced.ResolveZeroCredentialsForTerminalNodes(credentialType);
		}

		private DependencyGraph DrainZeroCredentials(RequestNode from, RequestNode to, CredentialType credentialType)
			=> this with { edgeSets = edgeSets.SetItem(credentialType, edgeSets[credentialType].DrainZeroCredentials(from, to)) };

		private DependencyGraph AddZeroCredential(RequestNode from, RequestNode to, CredentialType credentialType)
			=> this with { edgeSets = edgeSets.SetItem(credentialType, edgeSets[credentialType].AddZeroEdge(from, to)) };
	}
}

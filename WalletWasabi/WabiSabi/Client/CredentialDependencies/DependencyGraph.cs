using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using WalletWasabi.Extensions;

using AmountVsizePair = (long Amount, long Vsize);
namespace WalletWasabi.WabiSabi.Client.CredentialDependencies;
using AmountVsizePairArray = AmountVsizePair[];
using RequestNodeList = IEnumerable<RequestNode>;

[DebuggerDisplay("{AsGraphviz(),nq}")]
public record DependencyGraph
{
	public const int K = ProtocolConstants.CredentialNumber;

	public static ImmutableArray<CredentialType> CredentialTypes { get; } = [CredentialType.Amount, CredentialType.Vsize];

	public ImmutableList<RequestNode> Vertices { get; init; } = [];

	// Internal properties used to keep track of effective values and edges
	public ImmutableList<CredentialEdgeSet> EdgeSets { get; init; } = [ new AmountCredentialEdgeSet(), new VsizeCredentialEdgeSet() ];


	/// <summary>Construct a graph from amounts, and resolve the
	/// credential dependencies.</summary>
	///
	/// <remarks>Should only produce valid graphs. The elements of the
	/// <see>Vertices</see> property will correspond to the given values in order,
	/// and may contain additional nodes if reissuance requests are
	/// required.</remarks>
	///
	public static DependencyGraph ResolveCredentialDependencies(IEnumerable<Money> effectiveValues, IEnumerable<TxOut> outputs, FeeRate feeRate, IEnumerable<long> availableVSizes)
	{
		var effectiveValuesInSats = effectiveValues.Select(x => x.Satoshi);

		if (effectiveValues.Any(x => x <= Money.Zero))
		{
			throw new InvalidOperationException("Not enough funds to pay for the fees.");
		}

		var outputSizes = outputs.Select(x => (long)x.ScriptPubKey.EstimateOutputVsize());
		var effectiveCostsInSats = outputs.Select(txout => txout.EffectiveCost(feeRate).Satoshi);
		return ResolveCredentialDependencies(
			effectiveValuesInSats.Zip(availableVSizes).ToArray(),
			effectiveCostsInSats.Zip(outputSizes).ToArray()
		);
	}

	public static DependencyGraph ResolveCredentialDependencies(AmountVsizePairArray inputValues, AmountVsizePairArray outputValues)
		=> FromValues(inputValues, outputValues).ResolveCredentials();

	public static DependencyGraph FromValues(AmountVsizePairArray inputValues, AmountVsizePairArray outputValues)
	{
		var allValues = Enumerable.Concat(inputValues, outputValues);
		if (allValues.Any(x => x.Amount < 0 || x.Vsize < 0))
		{
			throw new ArgumentException("All values must be positive.");
		}

		foreach (var credentialType in CredentialTypes)
		{
			long ValueExtractor(AmountVsizePair x) => credentialType is CredentialType.Amount ? x.Amount : x.Vsize;
			var inputValuesSum = inputValues.Sum(ValueExtractor);
			var outputValuesSum = outputValues.Sum(ValueExtractor);
			if (inputValuesSum < outputValuesSum)
			{
				throw new ArgumentException("Overall balance must not be negative.");
			}
		}

		return new DependencyGraph()
			.AddNodes(inputValues, v => new InputNode(v.Amount, v.Vsize))
			.AddNodes(outputValues, v => new OutputNode(-v.Amount, -v.Vsize));
	}

	private DependencyGraph AddNode(RequestNode node)
		=> this with
		{
			Vertices = Vertices.Add(node),
			EdgeSets = EdgeSets.Select(
				x => x with
				{
					EdgeBalances = x.EdgeBalances.Add(node, 0),
					InEdges = x.InEdges.Add(node, ImmutableHashSet<CredentialDependency>.Empty),
					OutEdges = x.OutEdges.Add(node, ImmutableHashSet<CredentialDependency>.Empty)
				}
			).ToImmutableList(),
		};

	// Input nodes represent a combination of an input registration and
	// connection confirmation. Connection confirmation requests actually
	// have indegree K, not 0, which could be used to consolidate inputs
	// early but using it implies connection confirmations may have
	// dependencies, posing some complexity for a privacy preserving
	// approach.
	private DependencyGraph AddNodes(AmountVsizePairArray values, Func<AmountVsizePair, RequestNode> builder) =>
		values.Aggregate(this, (g, v) => g.AddNode(builder(v)));


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
	/// left over balance the outgoing degree is limited to K-1, since an
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
	/// including range proof).</para>
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
	/// <para>There are two related/composing special cases mainly affecting
	/// vsize credentials - when the positive valued nodes with remaining
	/// out degree > 1 have a uniform and sufficient balance to cover all of
	/// the negative balances, instead of taking the largest node all of the
	/// equal valued nodes are reduced together resulting in a more balanced
	/// structure overall. This is combined with another special case that
	/// checks if pairing positive and negative nodes in a 1:1
	/// correspondence is possible after aggregation into reissuance nodes.
	/// This can apply to amount credentials as well when consolidating
	/// multiple equal valued inputs, but is only expected to regularly
	/// occur for vbyte credentials.</para>
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
	private DependencyGraph ResolveCredentials() =>
		ResolveNegativeBalanceNodes(CredentialType.Amount)
		.ResolveNegativeBalanceNodes(CredentialType.Vsize)
		.ResolveZeroCredentials(CredentialType.Amount)
		.ResolveZeroCredentials(CredentialType.Vsize);

	private DependencyGraph ResolveNegativeBalanceNodes(CredentialType credentialType)
	{
		var g = ResolveUniformInputSpecialCases(credentialType);

		var edgeSet = g.EdgeSets[(int)credentialType];

		var positive = g.Vertices.Where(v => edgeSet.Balance(v) > 0);
		var negative = g.Vertices.Where(v => edgeSet.Balance(v) < 0);

		if (!negative.Any())
		{
			return g;
		}

		var (largestMagnitudeNode, smallMagnitudeNodes, fanIn) = edgeSet.MatchNodesToDischarge(positive, negative);

		var maxCount = fanIn
			? edgeSet.RemainingInDegree(largestMagnitudeNode)
			: edgeSet.RemainingOutDegree(largestMagnitudeNode);

		switch (Math.Abs(edgeSet.Balance(largestMagnitudeNode)).CompareTo(Math.Abs(smallMagnitudeNodes.Sum(x => edgeSet.Balance(x)))))
		{
			case 1:
				// When we are draining a positive valued node into multiple
				// negative nodes and we can't drain it completely, we need to
				// leave an edge unused for the remaining amount.
				// The corresponding condition can't actually happen for fan-in
				// because the negative balance of the last loop iteration can't
				// exceed the remaining positive elements, their total sum
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
					// otherwise, drain the largest magnitude node into a new
					// reissuance node which will have room for an unused edge
					// in its out edge set.
					(g, largestMagnitudeNode) = g.AggregateIntoReissuanceNode(new[] { largestMagnitudeNode }, credentialType);
				}
				break;

			case -1:
				// When the total amount is less, it means the last node
				// cannot be fully discharged, so we need to make sure its
				// remaining degree is > 1
				Func<RequestNode, int> smallNodeDegree = fanIn ? edgeSet.RemainingOutDegree : edgeSet.RemainingInDegree;

				// Order by degree, so that the nodes with only one
				// remaining edge slot are discharged first.
				smallMagnitudeNodes = smallMagnitudeNodes.OrderBy(smallNodeDegree);

				// If all of the nodes have degree 1 and the remaining
				// degree of the large magnitude node can cover all of them,
				// we must force a reissuance as well. Because the sum of
				// the small magnitude nodes is greater.
				if (smallMagnitudeNodes.Count() <= maxCount && smallNodeDegree(smallMagnitudeNodes.Last()) == 1)
				{
					// Make sure ReduceNodes will add at least one
					// reissuance node (they are appended so the remaining
					// degree for the last node is guaranteed to be > 1)
					maxCount = Math.Min(smallMagnitudeNodes.Count() - 1, maxCount);
				}
				break;

			default:
				// the large node and small nodes are exactly equal, so no
				// amounts will be left over.
				break;
		}

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
		var edgeSet = EdgeSets[(int)credentialType];

		// Evaluate the linq query eagerly since edgeSet is reassigned
		RequestNodeList negative = Vertices.Where(v => edgeSet.Balance(v) < 0).OrderBy(v => edgeSet.Balance(v)).ToImmutableArray();

		if (!negative.Any())
		{
			return this;
		}

		var g = this;

		// Unconstrained nodes have a remaining out degree greater than 1,
		// so they can produce arbitrary value outputs (final edge must leave
		// balance = 0).
		// The remaining outdegree > 1 condition is equivalent to == K for
		// K=2, so that also implies the positive valued nodes haven't been
		// used for this credential type yet.
		var unconstrainedPositive = Vertices.Where(v => edgeSet.Balance(v) > 0 && edgeSet.RemainingOutDegree(v) > 1).OrderByDescending(v => edgeSet.Balance(v)).ToImmutableArray();

		// First special case, if the unconstrained inputs have uniform values (should be the
		// common case for vbyte credentials)...
		if (unconstrainedPositive.Select(v => edgeSet.Balance(v)).Distinct().Count() == 1)
		{
			// And if the total amount of the uniform unconstrained nodes is
			// larger than the negative nodes, with room to spare
			if (unconstrainedPositive.Length * edgeSet.Balance(unconstrainedPositive.First()) > -1 * negative.Count() * edgeSet.Balance(negative.First()))
			{
				// Aggregate the negative nodes if they are more numerous.
				// Reducing to `unconstrainedPositive.Count()` (which can be
				// significantly larger than K) creates a more balanced
				// structure for the next special case, or later iterations.
				if (negative.Count() > unconstrainedPositive.Length)
				{
					(g, negative) = g.ReduceNodes(negative, unconstrainedPositive.Length, credentialType);
				}
			}
		}

		edgeSet = g.EdgeSets[(int)credentialType];

		// Second special case, more general than the previous one.
		// If negative nodes are all strictly smaller than the corresponding
		// positive nodes (not necessarily of uniform value), discharge them
		// in a 1:1 correspondence.
		if (negative.Count() <= unconstrainedPositive.Length && Enumerable.Zip(unconstrainedPositive, negative).All(p => edgeSet.Balance(p.First) + edgeSet.Balance(p.Second) >= 0))
		{
			g = Enumerable.Zip(unconstrainedPositive, negative).Aggregate(g, (g, p) => g.DrainTerminal(p.First, new RequestNode[] { p.Second }, credentialType));
		}

		return g;
	}

	// Build a k-ary tree bottom up to reduce a list of nodes to discharge
	// to at most maxCount elements.
	private (DependencyGraph, RequestNodeList) ReduceNodes(RequestNodeList nodes, int maxCount, CredentialType credentialType)
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

		// TODO order nodes by depth/height, so that this will produce a
		// balanced structure. Currently nodes are just merged into a binary
		// tree in the order given by the enumerable, but this can create an
		// imbalance if a small magnitude node is a reissuance node left
		// over from a previous iteration.
		var (g, reissuance) = AggregateIntoReissuanceNode(nodes.Take(take), credentialType);

		return g.ReduceNodes(nodes.Skip(take).Append(reissuance), maxCount, credentialType);
	}

	private (DependencyGraph, RequestNode) AggregateIntoReissuanceNode(RequestNodeList nodes, CredentialType credentialType)
	{
		var reissuance = new ReissuanceNode();
		var g = AddNode(reissuance);

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

	private DependencyGraph DrainReissuance(RequestNode reissuance, RequestNodeList nodes, CredentialType credentialType)
	{
		var drainedEdgeSet = EdgeSets[(int)credentialType].DrainReissuance(reissuance, nodes);

		var g = this with { EdgeSets = EdgeSets.SetItem((int)credentialType, drainedEdgeSet) };

		// Also drain all subsequent credential types, to minimize
		// dependencies between different requests, weight credentials
		// should often be easily satisfiable with parallel edges to the
		// amount credential edges.
		if (Enum.IsDefined(credentialType + 1))
		{
			// TODO Limit up to a certain height in the graph, no more than
			// the initial value, this can sometimes create a deeper graph
			// than necessary by being too greedy about consolidating vbyte
			// nodes and making them larger than the per input vbyte
			// allocation. Should be rare in practice though, so ignored for
			// now.
			return g.DrainReissuance(reissuance, nodes, credentialType + 1);
		}

		return g;
	}

	private DependencyGraph DrainTerminal(RequestNode node, RequestNodeList nodes, CredentialType credentialType)
		// Here we avoid opportunistically adding edges of other types as it
		// provides no benefit with K=2. Stable sorting prevents edge
		// crossing to a limited degree, but could be much better.
		=> this with { EdgeSets = EdgeSets.SetItem((int)credentialType, EdgeSets[(int)credentialType].DrainTerminal(node, nodes)) };

	private DependencyGraph ResolveZeroCredentials(CredentialType credentialType)
	{
		// TODO Build edges in parallel to existing ones to reduce
		// dependencies per request.
		// This can be done in 3 depth first passes, which all consume nodes
		// whose remaining in degree is 0 and available zero degree is >0.
		// - discharge only to AvailableZeroOutDegree >0 nodes (should be at most one such node per donor node)
		// - discharge to direct descendants even if a net reduction in zero creds because of available zero out degree of 0
		// - discharge all remaining in degree >0 nodes by topological order

		var edgeSet = EdgeSets[(int)credentialType];
		var unresolvedNodes = Vertices.Where(v => edgeSet.RemainingInDegree(v) > 0 && edgeSet.AvailableZeroOutDegree(v) > 0).OrderByDescending(v => edgeSet.AvailableZeroOutDegree(v));

		if (!unresolvedNodes.Any())
		{
			return ResolveZeroCredentialsForTerminalNodes(credentialType);
		}

		// Resolve remaining zero credentials by using nodes with no
		// dependencies but remaining out degree (following DAG order)
		var providers = Vertices.Where(v => edgeSet.RemainingInDegree(v) == 0 && edgeSet.AvailableZeroOutDegree(v) > 0)
			.SelectMany(v => Enumerable.Repeat(v, edgeSet.AvailableZeroOutDegree(v)));

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
		var edgeSet = EdgeSets[(int)credentialType];
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
		=> this with { EdgeSets = EdgeSets.SetItem((int)credentialType, EdgeSets[(int)credentialType].DrainZeroCredentials(from, to)) };

	private DependencyGraph AddZeroCredential(RequestNode from, RequestNode to, CredentialType credentialType)
		=> this with { EdgeSets = EdgeSets.SetItem((int)credentialType, EdgeSets[(int)credentialType].AddZeroEdge(from, to)) };

	private string AsGraphviz() => DependencyGraphExtensions.AsGraphviz(this);
}

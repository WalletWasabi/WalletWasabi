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

	// Internal properties used to keep track of effective values and edges.
	public ImmutableList<CredentialEdgeSet> EdgeSets { get; init; }

	private DependencyGraph(long maxAmountCredential, long maxVsizeCredential)
	{
		EdgeSets = [new AmountCredentialEdgeSet(maxAmountCredential), new VsizeCredentialEdgeSet(maxVsizeCredential)];
	}

	public static DependencyGraph ResolveCredentialDependencies(
		IEnumerable<Money> effectiveValues,
		IEnumerable<TxOut> outputs,
		FeeRate feeRate,
		IEnumerable<long> availableVSizes,
		long maxAmountCredential,
		long maxVsizeCredential)
	{
		var effectiveValuesArr = effectiveValues as Money[] ?? effectiveValues.ToArray();
		var outputsArr = outputs as TxOut[] ?? outputs.ToArray();
		var availableVSizesArr = availableVSizes as long[] ?? availableVSizes.ToArray();

		if (effectiveValuesArr.Any(x => x <= Money.Zero))
		{
			throw new InvalidOperationException("Not enough funds to pay for the fees.");
		}

		var effectiveValuesInSats = effectiveValuesArr.Select(x => x.Satoshi).ToArray();
		var outputSizes = outputsArr.Select(x => (long)x.ScriptPubKey.EstimateOutputVsize()).ToArray();
		var effectiveCostsInSats = outputsArr.Select(txout => txout.EffectiveCost(feeRate).Satoshi).ToArray();

		return ResolveCredentialDependencies(
			effectiveValuesInSats.Zip(availableVSizesArr).ToArray(),
			effectiveCostsInSats.Zip(outputSizes).ToArray(),
			maxAmountCredential,
			maxVsizeCredential);
	}

	public static DependencyGraph ResolveCredentialDependencies(
		AmountVsizePairArray inputValues,
		AmountVsizePairArray outputValues,
		long maxAmountCredential,
		long maxVsizeCredential)
		=> FromValues(inputValues, outputValues, maxAmountCredential, maxVsizeCredential).ResolveCredentials();

	public static DependencyGraph FromValues(
		AmountVsizePairArray inputValues,
		AmountVsizePairArray outputValues,
		long maxAmountCredential,
		long maxVsizeCredential)
	{
		var allValues = inputValues.Concat(outputValues).ToArray();
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

		return new DependencyGraph(maxAmountCredential, maxVsizeCredential)
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
				}).ToImmutableList(),
		};

	private DependencyGraph AddNodes(AmountVsizePairArray values, Func<AmountVsizePair, RequestNode> builder)
		=> values.Aggregate(this, (g, v) => g.AddNode(builder(v)));

	private DependencyGraph ResolveCredentials()
		=> ResolveNegativeBalanceNodes(CredentialType.Amount)
			.ResolveNegativeBalanceNodes(CredentialType.Vsize)
			.ResolveZeroCredentials(CredentialType.Amount)
			.ResolveZeroCredentials(CredentialType.Vsize);

	private DependencyGraph ResolveNegativeBalanceNodes(CredentialType credentialType)
	{
		var g = ResolveUniformInputSpecialCases(credentialType);
		var edgeSet = g.EdgeSets[(int)credentialType];

		var positive = g.Vertices.Where(v => edgeSet.Balance(v) > 0).ToArray();
		var negative = g.Vertices.Where(v => edgeSet.Balance(v) < 0).ToArray();

		if (negative.Length == 0)
		{
			return g;
		}

		var (largestMagnitudeNode, smallMagnitudeNodesRaw, fanIn) = edgeSet.MatchNodesToDischarge(positive, negative);
		var smallMagnitudeNodes = smallMagnitudeNodesRaw.ToArray();

		var maxCount = fanIn
			? edgeSet.RemainingInDegree(largestMagnitudeNode)
			: edgeSet.RemainingOutDegree(largestMagnitudeNode);

		var largestAbs = Math.Abs(edgeSet.Balance(largestMagnitudeNode));
		var smallAbs = Math.Abs(smallMagnitudeNodes.Sum(x => edgeSet.Balance(x)));

		switch (largestAbs.CompareTo(smallAbs))
		{
			case 1:
				if (maxCount > 1)
				{
					maxCount--;
				}
				else
				{
					(g, largestMagnitudeNode) = g.AggregateIntoReissuanceNode([largestMagnitudeNode], credentialType);
				}
				break;

			case -1:
				Func<RequestNode, int> smallNodeDegree = fanIn ? edgeSet.RemainingOutDegree : edgeSet.RemainingInDegree;
				smallMagnitudeNodes = smallMagnitudeNodes.OrderBy(smallNodeDegree).ToArray();

				if (smallMagnitudeNodes.Length <= maxCount && smallNodeDegree(smallMagnitudeNodes[^1]) == 1)
				{
					maxCount = Math.Min(smallMagnitudeNodes.Length - 1, maxCount);
				}
				break;
		}

		maxCount = Math.Max(0, maxCount);

		(g, var reducedNodes) = g.ReduceNodes(smallMagnitudeNodes, maxCount, credentialType);
		g = g.DrainTerminal(largestMagnitudeNode, reducedNodes, credentialType);

		return g.ResolveNegativeBalanceNodes(credentialType);
	}

	private DependencyGraph ResolveUniformInputSpecialCases(CredentialType credentialType)
	{
		var edgeSet = EdgeSets[(int)credentialType];
		var negative = Vertices
			.Where(v => edgeSet.Balance(v) < 0)
			.OrderBy(v => edgeSet.Balance(v))
			.ToArray();

		if (negative.Length == 0)
		{
			return this;
		}

		var g = this;

		var unconstrainedPositive = Vertices
			.Where(v => edgeSet.Balance(v) > 0 && edgeSet.RemainingOutDegree(v) > 1)
			.OrderByDescending(v => edgeSet.Balance(v))
			.ToArray();

		if (unconstrainedPositive.Length > 0)
		{
			var distinctPositiveBalances = unconstrainedPositive
				.Select(v => edgeSet.Balance(v))
				.Distinct()
				.Count();

			if (distinctPositiveBalances == 1)
			{
				var positiveTotal = unconstrainedPositive.Length * edgeSet.Balance(unconstrainedPositive[0]);
				var negativeTotalAbs = -negative.Length * edgeSet.Balance(negative[0]);

				if (positiveTotal > negativeTotalAbs && negative.Length > unconstrainedPositive.Length)
				{
					(g, var reducedNegative) = g.ReduceNodes(negative, unconstrainedPositive.Length, credentialType);
					negative = reducedNegative.ToArray();
				}
			}
		}

		edgeSet = g.EdgeSets[(int)credentialType];

		if (negative.Length <= unconstrainedPositive.Length &&
			unconstrainedPositive.Zip(negative).All(p => edgeSet.Balance(p.First) + edgeSet.Balance(p.Second) >= 0))
		{
			g = unconstrainedPositive
				.Zip(negative)
				.Aggregate(g, (acc, p) => acc.DrainTerminal(p.First, [p.Second], credentialType));
		}

		return g;
	}

	private (DependencyGraph, RequestNodeList) ReduceNodes(RequestNodeList nodes, int maxCount, CredentialType credentialType)
	{
		var nodeArray = nodes as RequestNode[] ?? nodes.ToArray();

		if (nodeArray.Length <= maxCount)
		{
			return (this, nodeArray);
		}

		var take = Math.Min(K, nodeArray.Length);
		var (g, reissuance) = AggregateIntoReissuanceNode(nodeArray.Take(take), credentialType);

		return g.ReduceNodes(nodeArray.Skip(take).Append(reissuance), maxCount, credentialType);
	}

	private (DependencyGraph, RequestNode) AggregateIntoReissuanceNode(RequestNodeList nodes, CredentialType credentialType)
	{
		var nodesArr = nodes as RequestNode[] ?? nodes.ToArray();

		var reissuance = new ReissuanceNode();
		var g = AddNode(reissuance);

		g = g.DrainReissuance(reissuance, nodesArr, credentialType);

		// Also discharge 0 credentials for previous credential type.
		if ((int)credentialType > 0)
		{
			g = nodesArr.Aggregate(g, (acc, v) => acc.DrainZeroCredentials(reissuance, v, (CredentialType)((int)credentialType - 1)));
		}

		return (g, reissuance);
	}

	private DependencyGraph DrainReissuance(RequestNode reissuance, RequestNodeList nodes, CredentialType credentialType)
	{
		var nodesArr = nodes as RequestNode[] ?? nodes.ToArray();

		return Enumerable
			.Range((int)credentialType, EdgeSets.Count - (int)credentialType)
			.Aggregate(
				this,
				(graph, i) => graph with
				{
					EdgeSets = graph.EdgeSets.SetItem(i, graph.EdgeSets[i].DrainReissuance(reissuance, nodesArr))
				});
	}

	private DependencyGraph DrainTerminal(RequestNode node, RequestNodeList nodes, CredentialType credentialType)
		=> this with
		{
			EdgeSets = EdgeSets.SetItem((int)credentialType, EdgeSets[(int)credentialType].DrainTerminal(node, nodes))
		};

	private DependencyGraph ResolveZeroCredentials(CredentialType credentialType)
		=> ResolveZeroCredentialsCore(credentialType, terminalOnly: false);

	private DependencyGraph ResolveZeroCredentialsCore(CredentialType credentialType, bool terminalOnly)
	{
		return Step(this, terminalOnly);

		DependencyGraph Step(DependencyGraph graph, bool terminalPass)
		{
			var edgeSet = graph.EdgeSets[(int)credentialType];

			var verticesWithRemainingInDegree = graph.Vertices.Where(v => edgeSet.RemainingInDegree(v) > 0);
			var unresolved = terminalPass
				? verticesWithRemainingInDegree
				: verticesWithRemainingInDegree.Where(v => edgeSet.AvailableZeroOutDegree(v) > 0);

			var unresolvedArr = unresolved
				.OrderByDescending(v => edgeSet.AvailableZeroOutDegree(v))
				.ToArray();

			if (unresolvedArr.Length == 0)
			{
				return terminalPass ? graph : Step(graph, terminalPass: true);
			}

			var providers = graph.Vertices
				.Where(v => edgeSet.RemainingInDegree(v) == 0 && edgeSet.AvailableZeroOutDegree(v) > 0)
				.SelectMany(v => Enumerable.Repeat(v, edgeSet.AvailableZeroOutDegree(v)))
				.ToArray();

			var next = unresolvedArr
				.SelectMany(v => Enumerable.Repeat(v, edgeSet.RemainingInDegree(v)))
				.Zip(providers, (to, from) => (from, to))
				.Aggregate(graph, (acc, pair) => acc.AddZeroCredential(pair.from, pair.to, credentialType));

			return Step(next, terminalPass);
		}
	}

	private DependencyGraph DrainZeroCredentials(RequestNode from, RequestNode to, CredentialType credentialType)
		=> this with { EdgeSets = EdgeSets.SetItem((int)credentialType, EdgeSets[(int)credentialType].DrainZeroCredentials(from, to)) };

	private DependencyGraph AddZeroCredential(RequestNode from, RequestNode to, CredentialType credentialType)
		=> this with { EdgeSets = EdgeSets.SetItem((int)credentialType, EdgeSets[(int)credentialType].AddZeroEdge(from, to)) };

	private string AsGraphviz() => DependencyGraphExtensions.AsGraphviz(this);
}

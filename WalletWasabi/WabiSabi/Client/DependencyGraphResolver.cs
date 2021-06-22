using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.WabiSabi.Crypto;

namespace WalletWasabi.WabiSabi.Client
{
	public class DependencyGraphResolver
	{
		public DependencyGraphResolver(
			DependencyGraph graph,
			ZeroCredentialPool zeroAmountCredentialPool,
			ZeroCredentialPool zeroVsizeCredentialPool)
		{
			Graph = graph;
			var allInEdges = Enum.GetValues<CredentialType>()
				.SelectMany(type => Enumerable.Concat(Graph.Reissuances, Graph.Outputs)
				.SelectMany(node => Graph.EdgeSets[type].InEdges(node)));
			DependencyTasks = allInEdges.ToDictionary(edge => edge, _ => new TaskCompletionSource<Credential>(TaskCreationOptions.RunContinuationsAsynchronously));

			ZeroAmountCredentialPool = zeroAmountCredentialPool;
			ZeroVsizeCredentialPool = zeroVsizeCredentialPool;
		}

		private DependencyGraph Graph { get; }
		public ZeroCredentialPool ZeroAmountCredentialPool { get; }
		public ZeroCredentialPool ZeroVsizeCredentialPool { get; }
		private Dictionary<CredentialDependency, TaskCompletionSource<Credential>> DependencyTasks { get; }

		public async Task<List<(Money Amount, Credential[] AmounCreds, Credential[] VsizeCreds)>> ResolveAsync(IEnumerable<AliceClient> aliceClients, BobClient bobClient, CancellationToken cancellationToken)
		{
			var aliceNodePairs = PairAliceClientAndRequestNodes(aliceClients, Graph);

			// Set the result for the inputs.
			foreach ((var aliceClient, var node) in aliceNodePairs)
			{
				foreach ((var edge, var credential) in Enumerable.Zip(Graph.OutEdges(node, CredentialType.Amount).Where(edge => edge.Value > 0), aliceClient.RealAmountCredentials))
				{
					DependencyTasks[edge].SetResult(credential);
				}

				foreach (var edge in Graph.OutEdges(node, CredentialType.Amount).Where(edge => edge.Value == 0))
				{
					DependencyTasks[edge].SetResult(ZeroAmountCredentialPool.GetZeroCredential());
				}

				foreach ((var edge, var credential) in Enumerable.Zip(Graph.OutEdges(node, CredentialType.Vsize).Where(edge => edge.Value > 0), aliceClient.RealVsizeCredentials))
				{
					DependencyTasks[edge].SetResult(credential);
				}

				foreach (var edge in Graph.OutEdges(node, CredentialType.Vsize).Where(edge => edge.Value == 0))
				{
					DependencyTasks[edge].SetResult(ZeroVsizeCredentialPool.GetZeroCredential());
				}
			}

			// Build tasks and link them together.
			List<SmartRequestNode> smartRequestNodes = new();
			List<Task> alltask = new();

			using CancellationTokenSource ctsOnError = new();
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsOnError.Token);

			foreach (var node in Graph.Reissuances)
			{
				var inputAmountEdgeTasks = Graph.InEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge].Task);
				var inputVsizeEdgeTasks = Graph.InEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge].Task);

				var outputAmountEdgeTaskCompSources = Graph.OutEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge]);
				var outputVsizeEdgeTaskCompSources = Graph.OutEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge]);

				var requestedAmounts = Graph.OutEdges(node, CredentialType.Amount).Select(edge => (long)edge.Value);
				var requestedVSizes = Graph.OutEdges(node, CredentialType.Vsize).Select(edge => (long)edge.Value);

				SmartRequestNode smartRequestNode = new(
					inputAmountEdgeTasks,
					inputVsizeEdgeTasks,
					outputAmountEdgeTaskCompSources,
					outputVsizeEdgeTaskCompSources);

				var task = smartRequestNode
					.StartAsync(bobClient, requestedAmounts, requestedVSizes, linkedCts.Token)
					.ContinueWith((t) =>
				{
					if (t.IsFaulted && t.Exception is { } exception)
					{
						// If one task is failing, cancel all the tasks and throw.
						ctsOnError.Cancel();
						throw exception;
					}
				}, linkedCts.Token);
				alltask.Add(task);
			}

			await Task.WhenAll(alltask).ConfigureAwait(false);

			var amountEdges = Graph.Outputs.SelectMany(node => Graph.InEdges(node, CredentialType.Amount));
			var vsizeEdges = Graph.Outputs.SelectMany(node => Graph.InEdges(node, CredentialType.Vsize));

			// Check if all tasks were finished, otherwise Task.Result will block.
			if (!amountEdges.Concat(vsizeEdges).All(edge => DependencyTasks[edge].Task.IsCompletedSuccessfully))
			{
				throw new InvalidOperationException("");
			}

			var amountCreds = amountEdges.Select(edge => DependencyTasks[edge].Task.Result).Where(cred => cred != null);
			var vsizeCreds = vsizeEdges.Select(edge => DependencyTasks[edge].Task.Result).Where(cred => cred != null);

			List<(Money, Credential[], Credential[])> outputs = new();

			foreach (var (amountCred, vsizeCred) in amountCreds.Zip(vsizeCreds))
			{
				outputs.Add((amountCred.Amount.ToMoney(), new[] { amountCred }, new[] { vsizeCred }));
			}

			return outputs;
		}

		private IEnumerable<(AliceClient AliceClient, RequestNode Node)> PairAliceClientAndRequestNodes(IEnumerable<AliceClient> aliceClients, DependencyGraph graph)
		{
			var inputNodes = graph.Inputs;

			if (aliceClients.Count() != inputNodes.Count)
			{
				throw new InvalidOperationException("Graph vs Alice inputs mismatch");
			}

			return aliceClients.OrderBy(alice => alice.Coin.Amount).Zip(inputNodes.OrderBy(node => node.Values[(int)CredentialType.Amount]));
		}
	}
}

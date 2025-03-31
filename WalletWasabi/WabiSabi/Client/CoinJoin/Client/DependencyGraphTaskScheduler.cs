using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client.CredentialDependencies;
using WalletWasabi.WabiSabi.Coordinator.Models;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class DependencyGraphTaskScheduler
{
	public DependencyGraphTaskScheduler(DependencyGraph graph)
	{
		_graph = graph;
		var allInEdges = Enum.GetValues<CredentialType>()
			.SelectMany(type => _graph.GetReissuances().Concat<RequestNode>(_graph.GetOutputs())
			.SelectMany(node => _graph.EdgeSets[(int)type].InEdges[node]));
		DependencyTasks = allInEdges.ToDictionary(edge => edge, _ => new TaskCompletionSource<Credential>(TaskCreationOptions.RunContinuationsAsynchronously));
	}

	private readonly DependencyGraph _graph;
	private Dictionary<CredentialDependency, TaskCompletionSource<Credential>> DependencyTasks { get; }

	private async Task CompleteConnectionConfirmationAsync(IEnumerable<AliceClient> aliceClients, BobClient bobClient, CancellationToken cancellationToken)
	{
		var aliceNodePairs = PairAliceClientAndRequestNodes(aliceClients, _graph);

		List<Task> connectionConfirmationTasks = new();

		using CancellationTokenSource ctsOnError = new();
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsOnError.Token);

		foreach ((var aliceClient, var node) in aliceNodePairs)
		{
			var amountEdgeTaskCompSources = _graph.OutEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge]);
			var vsizeEdgeTaskCompSources = _graph.OutEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge]);
			SmartRequestNode smartRequestNode = new(
				aliceClient.IssuedAmountCredentials.Take(ProtocolConstants.CredentialNumber).Select(Task.FromResult),
				aliceClient.IssuedVsizeCredentials.Take(ProtocolConstants.CredentialNumber).Select(Task.FromResult),
				amountEdgeTaskCompSources,
				vsizeEdgeTaskCompSources);

			var amountsToRequest = _graph.OutEdges(node, CredentialType.Amount).Select(e => e.Value);
			var vsizesToRequest = _graph.OutEdges(node, CredentialType.Vsize).Select(e => e.Value);

			// Although connection confirmation requests support k
			// credential requests, for now we only know which amounts to
			// request after connection confirmation has finished and the
			// final decomposition can be computed, so as a workaround we
			// unconditionally request the full amount in one credential and
			// then do an equivalent reissuance request for every connection
			// confirmation.
			var task = smartRequestNode
				.StartReissuanceAsync(bobClient, amountsToRequest, vsizesToRequest, linkedCts.Token)
				.ContinueWith(
				(t) =>
				{
					if (t.IsFaulted && t.Exception is { } exception)
					{
						// If one task is failing, cancel all the tasks and throw.
						ctsOnError.Cancel();
						throw exception;
					}
				},
				linkedCts.Token);

			connectionConfirmationTasks.Add(task);
		}

		await Task.WhenAll(connectionConfirmationTasks).ConfigureAwait(false);

		var amountEdges = _graph.GetInputs().SelectMany(node => _graph.OutEdges(node, CredentialType.Amount));
		var vsizeEdges = _graph.GetInputs().SelectMany(node => _graph.OutEdges(node, CredentialType.Vsize));

		// Check if all tasks were finished, otherwise Task.Result will block.
		if (!amountEdges.Concat(vsizeEdges).All(edge => DependencyTasks[edge].Task.IsCompletedSuccessfully))
		{
			throw new InvalidOperationException("Some Input nodes out-edges failed to complete.");
		}
	}

	public async Task StartReissuancesAsync(IEnumerable<AliceClient> aliceClients, Func<BobClient> bobClientFactory, CancellationToken cancellationToken)
	{
		var aliceNodePairs = PairAliceClientAndRequestNodes(aliceClients, _graph);

		// Build tasks and link them together.
		List<Task> allTasks = new()
		{
			// Temporary workaround because we don't yet have a mechanism to
			// propagate the final amounts to request amounts to AliceClient's
			// connection confirmation loop even though they are already known
			// after the final successful input registration, which may be well
			// before the connection confirmation phase actually starts.
			CompleteConnectionConfirmationAsync(aliceClients, bobClientFactory(), cancellationToken)
		};

		using CancellationTokenSource ctsOnError = new();
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsOnError.Token);

		foreach (var node in _graph.GetReissuances())
		{
			var inputAmountEdgeTasks = _graph.InEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge].Task);
			var inputVsizeEdgeTasks = _graph.InEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge].Task);

			var outputAmountEdgeTaskCompSources = _graph.OutEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge]);
			var outputVsizeEdgeTaskCompSources = _graph.OutEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge]);

			var requestedAmounts = _graph.OutEdges(node, CredentialType.Amount).Select(edge => edge.Value);
			var requestedVSizes = _graph.OutEdges(node, CredentialType.Vsize).Select(edge => edge.Value);

			SmartRequestNode smartRequestNode = new(
				inputAmountEdgeTasks,
				inputVsizeEdgeTasks,
				outputAmountEdgeTaskCompSources,
				outputVsizeEdgeTaskCompSources);

			var task = smartRequestNode
				.StartReissuanceAsync(bobClientFactory(), requestedAmounts, requestedVSizes, linkedCts.Token)
				.ContinueWith(
				(t) =>
				{
					if (t.IsFaulted && t.Exception is { } exception)
					{
						// If one task is failing, cancel all the tasks and throw.
						ctsOnError.Cancel();
						throw exception;
					}
				},
				linkedCts.Token);

			allTasks.Add(task);
		}

		await Task.WhenAll(allTasks).ConfigureAwait(false);

		var amountEdges = _graph.GetOutputs().SelectMany(node => _graph.InEdges(node, CredentialType.Amount));
		var vsizeEdges = _graph.GetOutputs().SelectMany(node => _graph.InEdges(node, CredentialType.Vsize));

		// Check if all tasks were finished, otherwise Task.Result will block.
		if (!amountEdges.Concat(vsizeEdges).All(edge => DependencyTasks[edge].Task.IsCompletedSuccessfully))
		{
			throw new InvalidOperationException("Some Output nodes in-edges failed to complete");
		}
	}

	public record OutputRegistrationError();
	public record UnknownError(Script ScriptPubKey) : OutputRegistrationError;
	public record AlreadyRegisteredScriptError(Script ScriptPubKey) : OutputRegistrationError;

	public async Task<Result<OutputRegistrationError[]>> StartOutputRegistrationsAsync(IEnumerable<TxOut> txOuts, Func<BobClient> bobClientFactory,
		ImmutableList<DateTimeOffset> outputRegistrationScheduledDates, CancellationToken cancellationToken)
	{
		using CancellationTokenSource ctsOnError = new();
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsOnError.Token);

		var nodes = _graph.GetOutputs().Select(node =>
		{
			var amountCredsToPresentTasks = _graph.InEdges(node, CredentialType.Amount).Select(edge => DependencyTasks[edge].Task);
			var vsizeCredsToPresentTasks = _graph.InEdges(node, CredentialType.Vsize).Select(edge => DependencyTasks[edge].Task);

			SmartRequestNode smartRequestNode = new(
				amountCredsToPresentTasks,
				vsizeCredsToPresentTasks,
				Array.Empty<TaskCompletionSource<Credential>>(),
				Array.Empty<TaskCompletionSource<Credential>>());
			return smartRequestNode;
		});

		var tasks = txOuts.Zip(
			nodes,
			outputRegistrationScheduledDates,
			async (txOut, smartRequestNode, scheduledDate) =>
			{
				try
				{
					var delay = scheduledDate - DateTimeOffset.UtcNow;
					if (delay > TimeSpan.Zero)
					{
						await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
					}
					await smartRequestNode.StartOutputRegistrationAsync(bobClientFactory(), txOut.ScriptPubKey, cancellationToken).ConfigureAwait(false);
					return Result<OutputRegistrationError>.Ok();
				}
				catch (WabiSabiProtocolException ex) when (ex.ErrorCode == WabiSabiProtocolErrorCode.AlreadyRegisteredScript)
				{
					Logger.LogDebug($"Output registration error, code:'{ex.ErrorCode}' message:'{ex.Message}'.");
					return new AlreadyRegisteredScriptError(txOut.ScriptPubKey);
				}
				catch (Exception ex)
				{
					Logger.LogInfo($"Output registration error message:'{ex.Message}'.");
					return new UnknownError(txOut.ScriptPubKey);
				}
			})
			.ToImmutableArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);
		return tasks.Select(x => x.Result).SequenceResults();
	}

	private IEnumerable<(AliceClient AliceClient, InputNode Node)> PairAliceClientAndRequestNodes(IEnumerable<AliceClient> aliceClients, DependencyGraph graph)
	{
		var inputNodes = graph.GetInputs();

		if (aliceClients.Count() != inputNodes.Count())
		{
			throw new InvalidOperationException("_graph vs Alice inputs mismatch");
		}

		return aliceClients.Zip(inputNodes);
	}
}

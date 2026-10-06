using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;

namespace WalletWasabi.WabiSabi.Client;

public class SmartRequestNode
{
	// Limit reissuance requests at the same time when coinjoining with multiple wallets to avoid overloading Tor.
	private const int MaxParallelReissuanceRequests = 10;

	private static readonly SemaphoreSlim SemaphoreSlim = new(MaxParallelReissuanceRequests);

	public SmartRequestNode(
		IEnumerable<Task<Credential>> inputAmountCredentialTasks,
		IEnumerable<Task<Credential>> inputVsizeCredentialTasks,
		IEnumerable<TaskCompletionSource<Credential>> outputAmountCredentialTasks,
		IEnumerable<TaskCompletionSource<Credential>> outputVsizeCredentialTasks)
	{
		AmountCredentialToPresentTasks = inputAmountCredentialTasks.ToArray();
		VsizeCredentialToPresentTasks = inputVsizeCredentialTasks.ToArray();
		AmountCredentialTasks = outputAmountCredentialTasks.ToArray();
		VsizeCredentialTasks = outputVsizeCredentialTasks.ToArray();
	}

	public IEnumerable<Task<Credential>> AmountCredentialToPresentTasks { get; }
	public IEnumerable<Task<Credential>> VsizeCredentialToPresentTasks { get; }
	public IEnumerable<TaskCompletionSource<Credential>> AmountCredentialTasks { get; }
	public IEnumerable<TaskCompletionSource<Credential>> VsizeCredentialTasks { get; }

	public async Task StartReissuanceAsync(BobClient bobClient, IEnumerable<long> amounts, IEnumerable<long> vsizes, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var requiredAmounts = amounts.ToArray();
		var requiredVsizes = vsizes.ToArray();
		if (requiredAmounts.Length != AmountCredentialTasks.Count() || requiredVsizes.Length != VsizeCredentialTasks.Count())
		{
			throw new InvalidOperationException("Credential requests must match their output dependencies.");
		}
		await Task.WhenAll(AmountCredentialToPresentTasks.Concat(VsizeCredentialToPresentTasks)).WaitAsync(cancellationToken).ConfigureAwait(false);
		IEnumerable<Credential> inputAmountCredentials = AmountCredentialToPresentTasks.Select(x => x.Result);
		IEnumerable<Credential> inputVsizeCredentials = VsizeCredentialToPresentTasks.Select(x => x.Result);
		var amountsToRequest = AddExtraCredentialRequests(requiredAmounts, inputAmountCredentials.Sum(x => x.Value));
		var vsizesToRequest = AddExtraCredentialRequests(requiredVsizes, inputVsizeCredentials.Sum(x => x.Value));

		(IEnumerable<Credential> RealAmountCredentials, IEnumerable<Credential> RealVsizeCredentials) result;

		await SemaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			result = await bobClient.ReissueCredentialsAsync(
				amountsToRequest,
				vsizesToRequest,
				inputAmountCredentials,
				inputVsizeCredentials,
				cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			SemaphoreSlim.Release();
		}

		// TODO keep the credentials that were not needed by the graph
		// Resolve both complete allocations before publishing any dependency.
		// Zip must never silently leave a promise unresolved after a successful request.
		var (amountCredentials, _) = SeparateExtraCredentials(result.RealAmountCredentials, requiredAmounts);
		var (vsizeCredentials, _) = SeparateExtraCredentials(result.RealVsizeCredentials, requiredVsizes);
		cancellationToken.ThrowIfCancellationRequested();

		foreach (var (tcs, credential) in AmountCredentialTasks.Zip(amountCredentials))
		{
			tcs.SetResult(credential);
		}
		foreach (var (tcs, credential) in VsizeCredentialTasks.Zip(vsizeCredentials))
		{
			tcs.SetResult(credential);
		}
	}

	public async Task StartOutputRegistrationAsync(
		BobClient bobClient,
		Script scriptPubKey,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await Task.WhenAll(AmountCredentialToPresentTasks.Concat(VsizeCredentialToPresentTasks)).WaitAsync(cancellationToken).ConfigureAwait(false);
		IEnumerable<Credential> inputAmountCredentials = AmountCredentialToPresentTasks.Select(x => x.Result);
		IEnumerable<Credential> inputVsizeCredentials = VsizeCredentialToPresentTasks.Select(x => x.Result);

		await bobClient.RegisterOutputAsync(
			scriptPubKey,
			inputAmountCredentials,
			inputVsizeCredentials,
			cancellationToken).ConfigureAwait(false);
	}

	private IEnumerable<long> AddExtraCredentialRequests(IEnumerable<long> valuesToRequest, long sum)
	{
		var nonZeroValues = valuesToRequest.Where(v => v > 0);

		if (nonZeroValues.Count() == ProtocolConstants.CredentialNumber)
		{
			return nonZeroValues;
		}

		var missing = sum - valuesToRequest.Sum();

		if (missing > 0)
		{
			nonZeroValues = nonZeroValues.Append(missing);
		}

		// Note that this does not include the implied zero credentials
		// which are unconditionally requested.
		var additionalZeros = ProtocolConstants.CredentialNumber - nonZeroValues.Count();

		return nonZeroValues.Concat(Enumerable.Repeat(0L, additionalZeros));
	}

	internal (IReadOnlyList<Credential> Required, IReadOnlyList<Credential> Extra) SeparateExtraCredentials(IEnumerable<Credential> issuedCredentials, IEnumerable<long> requiredValues)
	{
		var remaining = issuedCredentials.ToList();
		var requiredCredentials = new List<Credential>();
		foreach (var required in requiredValues)
		{
			var index = remaining.FindIndex(credential => credential.Value == required);
			if (index < 0)
			{
				throw new InvalidOperationException("Reissuance did not supply every required credential.");
			}
			requiredCredentials.Add(remaining[index]);
			remaining.RemoveAt(index);
		}
		return (requiredCredentials, remaining);
	}
}

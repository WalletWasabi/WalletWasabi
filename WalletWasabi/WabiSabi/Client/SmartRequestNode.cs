using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.ZeroKnowledge;

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
		AmountCredentialToPresentTasks = inputAmountCredentialTasks;
		VsizeCredentialToPresentTasks = inputVsizeCredentialTasks;
		AmountCredentialTasks = outputAmountCredentialTasks;
		VsizeCredentialTasks = outputVsizeCredentialTasks;
	}

	public IEnumerable<Task<Credential>> AmountCredentialToPresentTasks { get; }
	public IEnumerable<Task<Credential>> VsizeCredentialToPresentTasks { get; }
	public IEnumerable<TaskCompletionSource<Credential>> AmountCredentialTasks { get; }
	public IEnumerable<TaskCompletionSource<Credential>> VsizeCredentialTasks { get; }

	public async Task StartReissuanceAsync(BobClient bobClient, IEnumerable<long> amounts, IEnumerable<long> vsizes, CancellationToken cancellationToken)
	{
		await Task.WhenAll(AmountCredentialToPresentTasks.Concat(VsizeCredentialToPresentTasks)).ConfigureAwait(false);
		IEnumerable<Credential> inputAmountCredentials = AmountCredentialToPresentTasks.Select(x => x.Result);
		IEnumerable<Credential> inputVsizeCredentials = VsizeCredentialToPresentTasks.Select(x => x.Result);
		var amountsToRequest = AddExtraCredentialRequests(amounts, inputAmountCredentials.Sum(x => x.Value));
		var vsizesToRequest = AddExtraCredentialRequests(vsizes, inputVsizeCredentials.Sum(x => x.Value));

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
		var (amountCredentials, _) = SeparateExtraCredentials(result.RealAmountCredentials, amounts);
		var (vsizeCredentials, _) = SeparateExtraCredentials(result.RealVsizeCredentials, vsizes);

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
		await Task.WhenAll(AmountCredentialToPresentTasks.Concat(VsizeCredentialToPresentTasks)).ConfigureAwait(false);
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

	private (IEnumerable<Credential>, IEnumerable<Credential>) SeparateExtraCredentials(IEnumerable<Credential> issuedCredentials, IEnumerable<long> requiredValues)
	{
		var taggedCredentials = TagExtraCredentials(issuedCredentials, requiredValues).ToImmutableArray();

		return (
			taggedCredentials.Where(x => !x.IsExtra).Select(x => x.Credential),
			taggedCredentials.Where(x => x.IsExtra).Select(x => x.Credential)
		);
	}

	private IEnumerable<(bool IsExtra, Credential Credential)> TagExtraCredentials(IEnumerable<Credential> issuedCredentials, IEnumerable<long> requiredValues)
	{
		using var requiredEnumerator = requiredValues.GetEnumerator();
		using var issuedEnumerator = issuedCredentials.GetEnumerator();

		while (requiredEnumerator.MoveNext())
		{
			var required = requiredEnumerator.Current;
			while (issuedEnumerator.MoveNext())
			{
				var issued = issuedEnumerator.Current;
				var isExtra = issued.Value != required;

				yield return (isExtra, issued);

				if (!isExtra)
				{
					// Move to next required value
					break;
				}
			}
		}

		while (issuedEnumerator.MoveNext())
		{
			var issued = issuedEnumerator.Current;
			yield return (true, issued);
		}
	}
}

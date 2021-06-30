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
	public class SmartRequestNode
	{
		public SmartRequestNode(
			IEnumerable<Task<Credential>> inputAmountCredentialTasks,
			IEnumerable<Task<Credential>> inputVsizeCredentialTasks,
			IEnumerable<TaskCompletionSource<Credential>> outputAmountCredentialTasks,
			IEnumerable<TaskCompletionSource<Credential>> outputVsizeCredentialTasks,
			ZeroCredentialPool zeroAmountCredentialPool,
			ZeroCredentialPool zeroVsizeCredentialPool)
		{
			AmountCredentialToPresentTasks = inputAmountCredentialTasks;
			VsizeCredentialToPresentTasks = inputVsizeCredentialTasks;
			AmountCredentialTasks = outputAmountCredentialTasks;
			VsizeCredentialTasks = outputVsizeCredentialTasks;
			ZeroAmountCredentialPool = zeroAmountCredentialPool;
			ZeroVsizeCredentialPool = zeroVsizeCredentialPool;
		}

		public IEnumerable<Task<Credential>> AmountCredentialToPresentTasks { get; }
		public IEnumerable<Task<Credential>> VsizeCredentialToPresentTasks { get; }
		public IEnumerable<TaskCompletionSource<Credential>> AmountCredentialTasks { get; }
		public IEnumerable<TaskCompletionSource<Credential>> VsizeCredentialTasks { get; }
		public ZeroCredentialPool ZeroAmountCredentialPool { get; }
		public ZeroCredentialPool ZeroVsizeCredentialPool { get; }

		public async Task ConfirmConnectionTaskAsync(
			TimeSpan connectionConfirmationTimeout,
			AliceClient aliceClient,
			IEnumerable<long> amounts,
			IEnumerable<long> vsizes,
			Money effectiveValue,
			int vsizeValue,
			CancellationToken cancellationToken)
		{
			var amountsToRequest = AddExtraCredential(amounts, effectiveValue.Satoshi);
			var vsizesToRequest = AddExtraCredential(vsizes, vsizeValue);

			await aliceClient.ConfirmConnectionAsync(connectionConfirmationTimeout, amountsToRequest, vsizesToRequest, cancellationToken).ConfigureAwait(false);

			var amountCredentials = aliceClient.RealAmountCredentials.Take(amounts.Where(v => v != 0).Count());
			var vsizeCredentials = aliceClient.RealVsizeCredentials.Take(vsizes.Where(v => v != 0).Count());

			amountCredentials = amountCredentials.Concat(Enumerable.Range(0, AmountCredentialTasks.Count() - amountCredentials.Count()).Select(_ => ZeroAmountCredentialPool.GetZeroCredential()));
			vsizeCredentials = vsizeCredentials.Concat(Enumerable.Range(0, VsizeCredentialTasks.Count() - vsizeCredentials.Count()).Select(_ => ZeroVsizeCredentialPool.GetZeroCredential()));

			foreach ((TaskCompletionSource<Credential> tcs, Credential credential) in AmountCredentialTasks.Zip(amountCredentials))
			{
				tcs.SetResult(credential);
			}
			foreach ((TaskCompletionSource<Credential> tcs, Credential credential) in VsizeCredentialTasks.Zip(vsizeCredentials))
			{
				tcs.SetResult(credential);
			}
		}

		public async Task StartReissuanceAsync(BobClient bobClient, IEnumerable<long> amounts, IEnumerable<long> vsizes, CancellationToken cancellationToken)
		{
			await Task.WhenAll(AmountCredentialToPresentTasks.Concat(VsizeCredentialToPresentTasks)).ConfigureAwait(false);
			IEnumerable<Credential> inputAmountCredentials = AmountCredentialToPresentTasks.Select(x => x.Result);
			IEnumerable<Credential> inputVsizeCredentials = VsizeCredentialToPresentTasks.Select(x => x.Result);
			var amountsToRequest = AddExtraCredential(amounts, inputAmountCredentials);
			var vsizesToRequest = AddExtraCredential(vsizes, inputVsizeCredentials);

			(IEnumerable<Credential> RealAmountCredentials, IEnumerable<Credential> RealVsizeCredentials) result = await bobClient.ReissueCredentialsAsync(
				amountsToRequest,
				vsizesToRequest,
				inputAmountCredentials,
				inputVsizeCredentials,
				cancellationToken).ConfigureAwait(false);

			// TODO keep the credentials that were not needed by the graph
			// Strip out any credentials that have to be requested for the
			// remaining amount but do not have a corresponding edge in the
			// graph (because no request node depends on them.)
			var amountCredentials = result.RealAmountCredentials.Take(amounts.Count(v => v != 0));
			var vsizeCredentials = result.RealVsizeCredentials.Take(vsizes.Count(v => v != 0));

			// TODO remove
			// Add back any zero credentials that do have a corresponding edge
			// (and therefore a task completion source), so that the
			// `amountCredentials` enumerator matches the `amounts` enumerator.
			// Assumes that `amounts` is ordered by value descending, so zero
			// credentials are at the end. (same for vsizes).
			amountCredentials = amountCredentials.Concat(Enumerable.Range(0, AmountCredentialTasks.Count() - amountCredentials.Count()).Select(_ => ZeroAmountCredentialPool.GetZeroCredential()));
			vsizeCredentials = vsizeCredentials.Concat(Enumerable.Range(0, VsizeCredentialTasks.Count() - vsizeCredentials.Count()).Select(_ => ZeroVsizeCredentialPool.GetZeroCredential()));

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
			Money effectiveCost,
			Script scriptPubKey,
			CancellationToken cancellationToken)
		{
			await Task.WhenAll(AmountCredentialToPresentTasks.Concat(VsizeCredentialToPresentTasks)).ConfigureAwait(false);
			IEnumerable<Credential> inputAmountCredentials = AmountCredentialToPresentTasks.Select(x => x.Result);
			IEnumerable<Credential> inputVsizeCredentials = VsizeCredentialToPresentTasks.Select(x => x.Result);

			await bobClient.RegisterOutputAsync(
				effectiveCost,
				scriptPubKey,
				inputAmountCredentials,
				inputVsizeCredentials,
				cancellationToken).ConfigureAwait(false);
		}

		private IEnumerable<long> AddExtraCredential(IEnumerable<long> valuesToRequest, IEnumerable<Credential> presentedCredentials)
		{
			var nonZeroValues = valuesToRequest.Where(v => v > 0);

			if (nonZeroValues.Count() == ProtocolConstants.CredentialNumber)
			{
				return nonZeroValues;
			}

			var missing = presentedCredentials.Sum(cr => cr.Value) - valuesToRequest.Sum();

			if (missing > 0)
			{
				nonZeroValues = nonZeroValues.Append(missing);
			}

			var additionalZeros = ProtocolConstants.CredentialNumber - nonZeroValues.Count();

			return nonZeroValues.Concat(Enumerable.Repeat(0L, additionalZeros));
		}

		private IEnumerable<long> AddExtraCredential(IEnumerable<long> valuesToRequest, long sum)
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

			var additionalZeros = ProtocolConstants.CredentialNumber - nonZeroValues.Count();

			return nonZeroValues.Concat(Enumerable.Repeat(0L, additionalZeros));
		}
	}
}

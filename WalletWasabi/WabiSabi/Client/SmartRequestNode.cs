using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Client
{
	public class SmartRequestNode
	{
		public SmartRequestNode(
			IEnumerable<Task<Credential>> inputAmountCredentialTasks,
			IEnumerable<Task<Credential>> inputVsizeCredentialTasks,
			IEnumerable<TaskCompletionSource<Credential>> outputAmountCredentialTasks,
			IEnumerable<TaskCompletionSource<Credential>> outputVsizeCredentialTasks)
		{
			InputAmountCredentialTasks = inputAmountCredentialTasks;
			InputVsizeCredentialTasks = inputVsizeCredentialTasks;
			OutputAmountCredentialTasks = outputAmountCredentialTasks;
			OutputVsizeCredentialTasks = outputVsizeCredentialTasks;
		}

		public IEnumerable<Task<Credential>> InputAmountCredentialTasks { get; }
		public IEnumerable<Task<Credential>> InputVsizeCredentialTasks { get; }
		public IEnumerable<TaskCompletionSource<Credential>> OutputAmountCredentialTasks { get; }
		public IEnumerable<TaskCompletionSource<Credential>> OutputVsizeCredentialTasks { get; }

		public async Task StartAsync(BobClient bobClient, IEnumerable<long> amounts, IEnumerable<long> vsizes, CancellationToken cancellationToken)
		{
			await Task.WhenAll(InputAmountCredentialTasks.Concat(InputVsizeCredentialTasks)).ConfigureAwait(false);
			IEnumerable<Credential> inputAmountCredentials = InputAmountCredentialTasks.Select(x => x.Result).Where(x => x is { });
			IEnumerable<Credential> inputVsizeCredentials = InputVsizeCredentialTasks.Select(x => x.Result).Where(x => x is { });
			var amountsToRequest = AddExtraCredential(amounts, inputAmountCredentials);
			var vsizesToRequest = AddExtraCredential(vsizes, inputVsizeCredentials);

			(Credential[] RealAmountCredentials, Credential[] RealVsizeCredentials) result = await bobClient.ReissueCredentialsAsync(
				amountsToRequest,
				vsizesToRequest,
				inputAmountCredentials,
				inputVsizeCredentials,
				cancellationToken).ConfigureAwait(false);

			foreach ((TaskCompletionSource<Credential> tcs, Credential credential) in OutputAmountCredentialTasks.Zip(result.RealAmountCredentials))
			{
				tcs.SetResult(credential);
			}
			foreach ((TaskCompletionSource<Credential> tcs, Credential credential) in OutputVsizeCredentialTasks.Zip(result.RealVsizeCredentials))
			{
				tcs.SetResult(credential);
			}
		}

		private IEnumerable<long> AddExtraCredential(IEnumerable<long> valuesToRequest, IEnumerable<Credential> presentedCredentials)
		{
			var nonZeroValues = valuesToRequest.Where(v => v > 0);

			if (nonZeroValues.Count() == ProtocolConstants.CredentialNumber)
			{
				return nonZeroValues;
			}

			var missing = presentedCredentials.Sum(cr => (long)cr.Amount.ToUlong()) - valuesToRequest.Sum();

			if (missing > 0)
			{
				nonZeroValues = nonZeroValues.Append(missing);
			}

			var additionalZeros = ProtocolConstants.CredentialNumber - nonZeroValues.Count();

			return nonZeroValues.Concat(Enumerable.Repeat(0L, additionalZeros));
		}
	}
}

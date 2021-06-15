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
			(var amount1, var amount2) = AddExtraCredential(amounts, inputAmountCredentials);
			(var vsize1, var vsize2) = AddExtraCredential(vsizes, inputVsizeCredentials);

			(Credential[] RealAmountCredentials, Credential[] RealVsizeCredentials) result = await bobClient.ReissueCredentialsAsync(
				amount1,
				amount2,
				vsize1,
				vsize2,
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

		private (long value1, long value2) AddExtraCredential(IEnumerable<long> valuesToRequest, IEnumerable<Credential> presentedCredentials)
		{
			if (!valuesToRequest.Any())
			{
				throw new ArgumentException("No values to request.", nameof(valuesToRequest));
			}

			if (valuesToRequest.Where(v => v > 0).Count() == 2)
			{
				return (valuesToRequest.ElementAt(0), valuesToRequest.ElementAt(1));
			}

			List<long> result = new();
			var v = valuesToRequest.First(v => v > 0);
			result.Add(v);
			var missing = presentedCredentials.Sum(cr => (long)cr.Amount.ToUlong()) - valuesToRequest.Sum();
			if (missing > 0)
			{
				result.Add(missing);
			}
			else
			{
				result.Add(0);
			}
			return (result[0], result[1]);
		}
	}
}

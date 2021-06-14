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

			IEnumerable<Credential> inputAmountCredentials = InputAmountCredentialTasks.Select(x => x.Result);
			IEnumerable<Credential> inputVsizeCredentials = InputVsizeCredentialTasks.Select(x => x.Result);

			(Credential[] RealAmountCredentials, Credential[] RealVsizeCredentials) result = await bobClient.ReissueCredentialsAsync(
				amounts.ElementAtOrDefault(0),
				amounts.ElementAtOrDefault(1),
				vsizes.ElementAtOrDefault(0),
				vsizes.ElementAtOrDefault(1),
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
	}
}

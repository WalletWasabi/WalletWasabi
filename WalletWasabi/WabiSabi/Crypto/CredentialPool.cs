using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto
{

	/// <summary>
	/// Keeps a registry of credentials available.
	/// </summary>
	public class CredentialPool
	{
		private readonly static object syncObj = new(); 
		private readonly List<Credential> _availableCredentials = new();
		private readonly List<(long RequestedValue, TaskCompletionSource<Credential[]> RequestSource)> _waitingCredentialList = new();

		internal CredentialPool()
		{
		}

		/// <summary>
		/// Registers those credentials that were issued.
		/// </summary>
		/// <param name="credentials">Credentials received from the coordinator.</param>
		internal void UpdateCredentials(IEnumerable<Credential> credentials)
		{
			lock (syncObj)
			{
				foreach (var credential in credentials)
				{
					Send(credential);
				}
			}
		}

		internal Task<Credential[]> TakeAsync(long requestedValue,CancellationToken cancellationToken = default)
		{
			lock (syncObj)
			{
				if (TryGetMatch(requestedValue, out var credentials))
				{
					return Task.FromResult(credentials);
				}

				var tcs = new TaskCompletionSource<Credential[]>();
				_waitingCredentialList.Add((requestedValue, tcs));

				using (cancellationToken.Register(() => tcs.TrySetCanceled()))
				return tcs.Task;
			}
		}

		private void Send(Credential credential)
		{
			lock (syncObj)
			{
				_availableCredentials.Add(credential);

				var matchingCombinations = CredentialCombinations
					.Join(_waitingCredentialList, 
						x => x.Total, 
						x => x.RequestedValue, 
						(o, i) => (o.Credentials, i.RequestSource));

				if (matchingCombinations.FirstOrDefault() is { Credentials: not null } found)
				{
					foreach (var credentialToRemove in found.Credentials)
					{
						_availableCredentials.Remove(credentialToRemove);
					}
					found.RequestSource.TrySetResult(found.Credentials);
				}
			}
		}

		private IEnumerable<(Credential[] Credentials, long Total)> CredentialCombinations 
			=> _availableCredentials
				.CombinationsWithoutRepetition(ProtocolConstants.CredentialNumber)
				.Select(x => (Credentails: x.ToArray(), Total: x.Sum(y => (long)y.Amount.ToUlong())));

		private bool TryGetMatch(long requestedValue, out Credential[] credentials)
		{
			if (CredentialCombinations.FirstOrDefault(x => x.Total == requestedValue) is {Credentials: not null} found )
			{
				credentials = found.Credentials;
				foreach (var credentialToRemove in credentials)
				{
					_availableCredentials.Remove(credentialToRemove);
				}

				return true;
			}
			credentials = null;
			return false;
		}
	}
}

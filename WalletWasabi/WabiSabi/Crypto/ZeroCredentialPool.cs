using System;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;
using System.Collections.Concurrent;
using System.Threading;

namespace WalletWasabi.WabiSabi.Crypto
{
	/// <summary>
	/// Keeps a registry of credentials available.
	/// </summary>
	public class ZeroCredentialPool
	{
		private readonly ConcurrentQueue<Credential> _zeroValueCredentials = new();

		internal ZeroCredentialPool()
		{
		}

		/// <summary>
		/// Registers those zero value credentials that were issued and return the valuable credentials.
		/// </summary>
		/// <param name="credentials">Credentials received from the coordinator.</param>
		public IEnumerable<Credential> ProcessAndGetValuableCredentials(IEnumerable<Credential> credentials)
		{
			foreach (var credential in credentials)
			{
				if (credential.Value == 0)
				{
					_zeroValueCredentials.Enqueue(credential);
				}
				else
				{
					yield return credential;
				}
			}
		}

		/// <summary>
		/// Given `c` credentials returns `k` credentials taking `k-c` null credentials from the pool.
		/// </summary>
		/// <param name="credentials">Credentials received from the coordinator.</param>
		public Credential[] FillOutWithZeroCredentials(IEnumerable<Credential> credentials, CancellationToken cancellationToken)
		{
			var n = ProtocolConstants.CredentialNumber;
			var credentialsToReturn = new Credential[ProtocolConstants.CredentialNumber];
			foreach (var credential in credentials.Take(ProtocolConstants.CredentialNumber).ToArray())
			{
				n--;
				credentialsToReturn[n] = credential;
			}

			while (n > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (_zeroValueCredentials.TryDequeue(out var credential))
				{
					n--;
					credentialsToReturn[n] = credential;
				}
			}
			return credentialsToReturn;
		}

		public Credential GetZeroCredential()
		{
			if (_zeroValueCredentials.TryDequeue(out var credential))
			{
				return credential;
			}
			else
			{
				throw new InvalidOperationException("ran out of zero credentials");
			}
		}
	}
}

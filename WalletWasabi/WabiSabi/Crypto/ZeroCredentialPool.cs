using System;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Crypto
{
	/// <summary>
	/// Keeps a registry of credentials available.
	/// </summary>
	public class ZeroCredentialPool
	{
		private readonly object syncObject = new();
		private readonly Queue<Credential> _zeroValueCredentials = new ();

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
				if (credential.Amount.IsZero)
				{
					lock (syncObject)
					{
						_zeroValueCredentials.Enqueue(credential);
					}
				}
				else
				{
					yield return credential;
				}
			}
		}

		public Credential[] FillOutWithZeroCredentials(IEnumerable<Credential> credentials)
		{
			var n = ProtocolConstants.CredentialNumber;
			var credentialsToReturn = new Credential[ProtocolConstants.CredentialNumber];
			foreach (var credential in credentials.Take(ProtocolConstants.CredentialNumber).ToArray())
			{
				n--;
				credentialsToReturn[n] = credential;
			}

			lock (syncObject)
			{
				while (n > 0)
				{
					if (!_zeroValueCredentials.TryDequeue(out var credential))
					{
						throw new InvalidOperationException("There are not enough null credentials.");
					}
					n--;
					credentialsToReturn[n] = credential;
				}
			}
			return credentialsToReturn;
		}
	}
}

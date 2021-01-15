using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi
{
	/// <summary>
	/// Keeps a registry of credentials available.
	/// </summary>
	public class CredentialPool
	{
		internal CredentialPool()
		{
		}

		private HashSet<Credential> Credentials { get; } = new HashSet<Credential>();

		/// <summary>
		/// Enumerates all the zero-value credentials available.
		/// </summary>
		public IEnumerable<Credential> ZeroValue => Credentials.Where(x => x.Amount.IsZero);

		/// <summary>
		/// Enumerates all the available credentials with non-zero value.
		/// </summary>
		public IEnumerable<Credential> Valuable => Credentials.Where(x => !x.Amount.IsZero);

		/// <summary>
		/// Enumerates all the available credentials.
		/// </summary>
		public IEnumerable<Credential> All => Credentials;

		/// <summary>
		/// Removes credentials that were used and registers those that were issued.
		/// </summary>
		/// <param name="newCredentials">Credentials received from the coordinator.</param>
		/// <param name="oldCredentials">Credentials exchanged by the ones that were issued.</param>
		internal void UpdateCredentials(IEnumerable<Credential> newCredentials, IEnumerable<Credential> oldCredentials)
		{
			var hs = oldCredentials.ToHashSet();
			Credentials.RemoveWhere(x => hs.Contains(x));

			foreach (var credential in newCredentials)
			{
				Credentials.Add(credential);
			}
		}
	}
}

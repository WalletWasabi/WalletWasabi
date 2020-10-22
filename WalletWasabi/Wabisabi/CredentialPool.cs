using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.Wabisabi
{
	public class CredentialPool
	{
		internal CredentialPool()
		{
		}

		private HashSet<Credential> Credentials { get; } = new HashSet<Credential>();

		public IEnumerable<Credential> ZeroValue => Credentials.Where(x => x.Amount.IsZero);

		public IEnumerable<Credential> Valuable => Credentials.Where(x => !x.Amount.IsZero);

		public IEnumerable<Credential> All => Credentials;

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
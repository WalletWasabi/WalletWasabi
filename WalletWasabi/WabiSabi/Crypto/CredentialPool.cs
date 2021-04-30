using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WalletWasabi.Crypto.ZeroKnowledge;
using System;
using NBitcoin;

namespace WalletWasabi.WabiSabi.Crypto
{
	/// <summary>
	/// Keeps a registry of credentials available.
	/// </summary>
	public class CredentialPool
	{
		private readonly static object syncObj = new(); 
		private readonly Dictionary<uint256, UnboundedChannel<Credential>> _channelsByRequester = new ();

		internal CredentialPool()
		{
		}

		/// <summary>
		/// Registers those credentials that were issued.
		/// </summary>
		/// <param name="credentials">Credentials received from the coordinator.</param>
		internal void UpdateCredentials(uint256 requesterId, IEnumerable<Credential> credentials)
		{
			lock (syncObj)
			{
				var channel = _channelsByRequester[requesterId];
				foreach (var credential in credentials)
				{
					channel.Send(credential);
				}
			}
		}

		internal Task<Credential[]> GetCredentialsForRequesterAsync(uint256 requesterId, CancellationToken cancellationToken = default)
		{
			lock (syncObj)
			{
				var channel = _channelsByRequester[requesterId];
				var tasks = Enumerable.Range(0, ProtocolConstants.CredentialNumber)
							.Select(_ => channel.TakeAsync(cancellationToken));

				return Task.WhenAll(tasks);
			}
		}
	}
}

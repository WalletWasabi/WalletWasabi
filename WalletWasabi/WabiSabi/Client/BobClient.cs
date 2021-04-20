using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Client
{
	public class BobClient
	{
		public BobClient(Guid roundId, ArenaClient arenaClient)
		{
			RoundId = roundId;
			ArenaClient = arenaClient;
		}

		private Guid RoundId { get; }
		private ArenaClient ArenaClient { get; }

		public async Task RegisterOutputAsync(Money amount, Script scriptPubKey)
		{
			await ArenaClient.RegisterOutputAsync(
				RoundId,
				amount.Satoshi,
				scriptPubKey,
				ArenaClient.AmountCredentialClient.Credentials.Valuable,
				ArenaClient.VsizeCredentialClient.Credentials.Valuable).ConfigureAwait(false);
		}
	}
}

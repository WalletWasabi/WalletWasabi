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
		public BobClient(uint256 id, uint256 roundId, ArenaClient arenaClient)
		{
			Id = id;
			RoundId = roundId;
			ArenaClient = arenaClient;
		}

		public uint256 Id { get; }
		private uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }

		public async Task RegisterOutputAsync(Money amount, Script scriptPubKey)
		{
			await ArenaClient.RegisterOutputAsync(
				Id,
				RoundId,
				amount.Satoshi,
				scriptPubKey,
				await ArenaClient.AmountCredentialClient.Credentials.GetCredentialsForRequesterAsync(Id),
				await ArenaClient.VsizeCredentialClient.Credentials.GetCredentialsForRequesterAsync(Id)).ConfigureAwait(false);
		}
	}
}

using NBitcoin;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Client
{
	public class BobClient
	{
		public BobClient(uint256 roundId, ArenaClient arenaClient)
		{
			RoundId = roundId;
			ArenaClient = arenaClient;
		}

		private uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }

		public async Task RegisterOutputAsync(Money amount, Script scriptPubKey, CancellationToken cancellationToken)
		{
			await ArenaClient.RegisterOutputAsync(
				RoundId,
				amount.Satoshi,
				scriptPubKey,
				ArenaClient.AmountCredentialClient.Credentials.Valuable,
				ArenaClient.VsizeCredentialClient.Credentials.Valuable,
				cancellationToken).ConfigureAwait(false);
		}
	}
}

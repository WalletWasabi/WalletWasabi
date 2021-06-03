using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Client
{
	public class BobClient
	{
		public BobClient(uint256 roundId, ArenaClient arenaClient)
		{
			RoundId = roundId;
			ArenaClient = arenaClient;
		}

		public uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }

		public async Task RegisterOutputAsync(Money amount, Script scriptPubKey, IEnumerable<Credential> amountCredential, IEnumerable<Credential> vsizeCredential, CancellationToken cancellationToken)
		{
			// TODO: what to do with the credentials returned?
			var response = await ArenaClient.RegisterOutputAsync(
				RoundId,
				amount.Satoshi,
				scriptPubKey,
				amountCredential,
				vsizeCredential,
				cancellationToken).ConfigureAwait(false);
		}

		public async Task<ArenaResponse> ReissuanceAsync(Money amount1, int scriptPubKeyVsize1, Money amount2, int scriptPubKeyVsize2, IEnumerable<Credential> amountCredential, IEnumerable<Credential> vsizeCredential, CancellationToken cancellationToken)
		{
			return await ArenaClient.ReissueCredentialAsync(
				RoundId,
				amount1.Satoshi,
				scriptPubKeyVsize1,
				amount2.Satoshi,
				scriptPubKeyVsize2,
				amountCredential,
				vsizeCredential,
				cancellationToken).ConfigureAwait(false);
		}
	}
}

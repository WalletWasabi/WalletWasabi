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

		public async Task<(Credential[] RealAmountCredentials, Credential[] RealVsizeCredentials)> ReissueCredentialsAsync(
			long amount1,
			long amount2,
			long vsize1,
			long vsize2,
			IEnumerable<Credential> amountCredential,
			IEnumerable<Credential> vsizeCredential,
			CancellationToken cancellationToken)
		{
			var response = await ArenaClient.ReissueCredentialAsync(
				RoundId,
				new[] { amount1, amount2 },
				new[] { vsize1,	vsize2 },
				amountCredential,
				vsizeCredential,
				cancellationToken)
				.ConfigureAwait(false);

			return (response.RealAmountCredentials, response.RealVsizeCredentials);
		}
	}
}

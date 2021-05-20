using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Client
{
	public class BobClient
	{
		public BobClient(uint256 roundId, ArenaClient arenaClient, IEnumerable<Credential> realAmountCredentials, IEnumerable<Credential> realVsizeCredentials)
		{
			RoundId = roundId;
			ArenaClient = arenaClient;
			RealAmountCredentials = realAmountCredentials.ToArray();
			RealVsizeCredentials = realVsizeCredentials.ToArray();
		}

		private uint256 RoundId { get; }
		private ArenaClient ArenaClient { get; }
		private Credential[] RealAmountCredentials { get; set; }
		private Credential[] RealVsizeCredentials { get; set; }

		public async Task RegisterOutputAsync(Money amount, Script scriptPubKey, CancellationToken cancellationToken)
		{
			// TODO: what to do with the credentials returned?
			var response = await ArenaClient.RegisterOutputAsync(
				RoundId,
				amount.Satoshi,
				scriptPubKey,
				RealAmountCredentials,
				RealVsizeCredentials,
				cancellationToken).ConfigureAwait(false);

			RealAmountCredentials = response.RealAmountCredentials;
			RealVsizeCredentials = response.RealVsizeCredentials;
		}
	}
}

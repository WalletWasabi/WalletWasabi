using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.Crypto.ZeroKnowledge;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client;

public class BobClient
{
	public BobClient(uint256 roundId, ArenaClient arenaClient)
	{
		RoundId = roundId;
		_arenaClient = arenaClient;
	}

	public uint256 RoundId { get; }
	private readonly ArenaClient _arenaClient;

	public async Task RegisterOutputAsync(Script scriptPubKey, IEnumerable<Credential> amountCredentials, IEnumerable<Credential> vsizeCredentials, CancellationToken cancellationToken)
	{
		await _arenaClient.RegisterOutputAsync(
			RoundId,
			scriptPubKey,
			amountCredentials,
			vsizeCredentials,
			cancellationToken).ConfigureAwait(false);
	}

	public async Task<(IEnumerable<Credential> RealAmountCredentials, IEnumerable<Credential> RealVsizeCredentials)> ReissueCredentialsAsync(
		IEnumerable<long> amountsToRequest,
		IEnumerable<long> vsizesToRequest,
		IEnumerable<Credential> amountCredential,
		IEnumerable<Credential> vsizeCredential,
		CancellationToken cancellationToken)
	{
		var response = await _arenaClient.ReissueCredentialAsync(
			RoundId,
			amountsToRequest,
			vsizesToRequest,
			amountCredential,
			vsizeCredential,
			cancellationToken)
			.ConfigureAwait(false);

		return (response.IssuedAmountCredentials, response.IssuedVsizeCredentials);
	}
}

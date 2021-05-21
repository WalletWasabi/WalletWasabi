using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Models
{
	public record RoundState(
		uint256 Id,
		CredentialIssuerParameters AmountCredentialIssuerParameters,
		CredentialIssuerParameters VsizeCredentialIssuerParameters,
		FeeRate FeeRate,
		MultipartyTransactionState CoinjoinState)
	{
		public static RoundState FromRound(Round round) =>
			new RoundState(round.Id, round.AmountCredentialIssuerParameters, round.VsizeCredentialIssuerParameters, round.FeeRate, round.CoinjoinState);
	}
}

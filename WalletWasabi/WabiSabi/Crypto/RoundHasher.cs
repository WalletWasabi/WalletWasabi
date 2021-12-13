using System.Collections.Immutable;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Crypto
{
	public static class RoundHasher
	{
		public static uint256 CalculateHash(
				DateTimeOffset inputRegistrationStart,
				TimeSpan inputRegistrationTimeout,
				TimeSpan connectionConfirmationTimeout,
				TimeSpan outputRegistrationTimeout,
				TimeSpan transactionSigningTimeout,
				MoneyRange allowedInputAmounts,
				ImmutableSortedSet<ScriptType> allowedInputTypes,
				MoneyRange allowedOutputAmounts,
				ImmutableSortedSet<ScriptType> allowedOutputTypes,
				Network network,
				long feePerK,
				int maxTransactionSize,
				long minRelayTxFeePerK,
				long maxAmountCredentialValue,
				long maxVsizeCredentialValue,
				long maxVsizeAllocationPerAlice,
				CredentialIssuerParameters amountCredentialIssuerParameters,
				CredentialIssuerParameters vsizeCredentialIssuerParameters)
				=> StrobeHasher.Create(ProtocolConstants.RoundStrobeDomain)
					.Append(ProtocolConstants.RoundInputRegistrationStartStrobeLabel, inputRegistrationStart)
					.Append(ProtocolConstants.RoundInputRegistrationTimeoutStrobeLabel, inputRegistrationTimeout)
					.Append(ProtocolConstants.RoundConnectionConfirmationTimeoutStrobeLabel, connectionConfirmationTimeout)
					.Append(ProtocolConstants.RoundOutputRegistrationTimeoutStrobeLabel, outputRegistrationTimeout)
					.Append(ProtocolConstants.RoundTransactionSigningTimeoutStrobeLabel, transactionSigningTimeout)
					.Append(ProtocolConstants.RoundAllowedInputAmountsStrobeLabel, allowedInputAmounts)
					.Append(ProtocolConstants.RoundAllowedInputTypesStrobeLabel, allowedInputTypes)
					.Append(ProtocolConstants.RoundAllowedOutputAmountsStrobeLabel, allowedOutputAmounts)
					.Append(ProtocolConstants.RoundAllowedOutputTypesStrobeLabel, allowedOutputTypes)
					.Append(ProtocolConstants.RoundNetworkStrobeLabel, network.ToString())
					.Append(ProtocolConstants.RoundFeeRateStrobeLabel, feePerK)
					.Append(ProtocolConstants.RoundMaxTransactionSizeStrobeLabel, maxTransactionSize)
					.Append(ProtocolConstants.RoundMinRelayTxFeeStrobeLabel, minRelayTxFeePerK)
					.Append(ProtocolConstants.RoundMaxAmountCredentialValueStrobeLabel, maxAmountCredentialValue)
					.Append(ProtocolConstants.RoundMaxVsizeCredentialValueStrobeLabel, maxVsizeCredentialValue)
					.Append(ProtocolConstants.RoundMaxVsizePerAliceStrobeLabel, maxVsizeAllocationPerAlice)
					.Append(ProtocolConstants.RoundAmountCredentialIssuerParametersStrobeLabel, amountCredentialIssuerParameters)
					.Append(ProtocolConstants.RoundVsizeCredentialIssuerParametersStrobeLabel, vsizeCredentialIssuerParameters)
					.GetHash();
	}
}
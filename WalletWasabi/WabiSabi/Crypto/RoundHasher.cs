using System;
using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.StrobeProtocol;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Crypto
{
	public static class RoundHasher
	{
		public static uint256 CalculateHash(
				TimeSpan inputRegistrationTimeout,
				TimeSpan connectionConfirmationTimeout,
				TimeSpan outputRegistrationTimeout,
				TimeSpan transactionSigningTimeout,
				MoneyRange allowedInputAmounts,
				IEnumerable<ScriptType> allowedInputTypes,
				MoneyRange allowedOutputAmounts,
				IEnumerable<ScriptType> allowedOutputTypes,
				Network network,
				long feePerK,
				int maxTransactionSize,
				long minRelayTxFeePerK,
				long maxRegistrableVsize,
				long maxVsizeAllocationPerAlice,
				CredentialIssuerParameters amountCredentialIssuerParameters,
				CredentialIssuerParameters vsizeCredentialIssuerParameters)
				=> StrobeHasher.Create(ProtocolConstants.RoundStrobeDomain)
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
					.Append(ProtocolConstants.RoundMaxRegistrableVsizeStrobeLabel, maxRegistrableVsize)
					.Append(ProtocolConstants.RoundMaxVsizePerAliceStrobeLabel, maxVsizeAllocationPerAlice)
					.Append(ProtocolConstants.RoundAmountCredentialIssuerParametersStrobeLabel, amountCredentialIssuerParameters)
					.Append(ProtocolConstants.RoundVsizeCredentialIssuerParametersStrobeLabel, vsizeCredentialIssuerParameters)
					.GetHash();
	}
}
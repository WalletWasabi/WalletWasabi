namespace WalletWasabi.WabiSabi;

public static class ProtocolConstants
{
	public const int CredentialNumber = 2;
	public const long MaxAmountPerAlice = 4_300_000_000_000L;
	public const long MaxVsizeCredentialValue = 255;

	public const string WabiSabiProtocolIdentifier = "WabiSabi_v1.0";
	public const string DomainStrobeSeparator = "domain-separator";

	// Round hashing labels
	public const string RoundStrobeDomain = "round-parameters";

	public const string RoundAllowedInputAmountsStrobeLabel = "allowed-input-amounts";
	public const string RoundAllowedOutputAmountsStrobeLabel = "allowed-output-amounts";
	public const string RoundAllowedInputTypesStrobeLabel = "allowed-input-types";
	public const string RoundAllowedOutputTypesStrobeLabel = "allowed-output-types";
	public const string RoundNetworkStrobeLabel = "network";
	public const string RoundMaxTransactionSizeStrobeLabel = "max-transaction-size";
	public const string RoundMinRelayTxFeeStrobeLabel = "min-relay-tx-fee";

	public const string RoundMaxAmountCredentialValueStrobeLabel = "maximum-amount-credential-value";
	public const string RoundMaxVsizeCredentialValueStrobeLabel = "maximum-vsize-credential-value";
	public const string RoundMaxVsizePerAliceStrobeLabel = "per-alice-vsize-allocation";
	public const string RoundAmountCredentialIssuerParametersStrobeLabel = "amount-credential-issuer-parameters";
	public const string RoundVsizeCredentialIssuerParametersStrobeLabel = "vsize-credential-issuer-parameters";
	public const string RoundFeeRateStrobeLabel = "fee-rate";
	public const string RoundCoordinationFeeRateStrobeLabel = "coordination-fee-rate";
	public const string RoundInputRegistrationStartStrobeLabel = "input-registration-start";
	public const string RoundInputRegistrationTimeoutStrobeLabel = "input-registration-timeout";
	public const string RoundConnectionConfirmationTimeoutStrobeLabel = "connection-confirmation-timeout";
	public const string RoundOutputRegistrationTimeoutStrobeLabel = "output-registration-timeout";
	public const string RoundTransactionSigningTimeoutStrobeLabel = "transaction-signing-timeout";
	public const string RoundMaxSuggestedAmountLabel = "maximum-suggested-amount";
	public const string RoundCoordinationIdentifier = "coordination-identifier";

	// Alice hashing labels
	public const string AliceStrobeDomain = "alice-parameters";

	public const string AliceCoinTxOutStrobeLabel = "coin-txout";
	public const string AliceCoinOutpointStrobeLabel = "coin-outpoint";
	public const string AliceOwnershipProofStrobeLabel = "ownership-proof";
	public const string ProtocolViolationType = "wabisabi-protocol-violation";
}

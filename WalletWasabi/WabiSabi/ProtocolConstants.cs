namespace WalletWasabi.WabiSabi;

public static class ProtocolConstants
{
	public const int CredentialNumber = 2;
	public const long MaxVsizeCredentialValue = 255;

	// Upper bounds used to reject oversized credential requests during deserialization,
	// before any per-element elliptic-curve work. A range proof over the full supply needs
	// at most MaxRangeProofWidth bits, giving 2w+1 equations (public nonces) and 3w+1
	// witnesses (responses). A request carries CredentialNumber presentations and requests,
	// and one proof per statement: CredentialNumber show proofs, CredentialNumber range
	// proofs and one balance proof.
	public const int MaxRangeProofWidth = 51;
	public const int MaxProofNonces = 2 * MaxRangeProofWidth + 1;
	public const int MaxProofResponses = 3 * MaxRangeProofWidth + 1;
	public const int MaxProofsPerRequest = 2 * CredentialNumber + 1;

	// A status request carries one checkpoint per round the client tracks, which mirrors the
	// coordinator's live round set. With default parallelization plus blame rounds and the
	// ended-round retention window that set stays in the low tens, so this bound rejects a
	// grossly oversized unauthenticated checkpoint array while leaving ample headroom.
	public const int MaxRoundCheckpoints = 1000;

	// Upper bound on a coordinator request body, enforced before deserialization. The largest
	// request permitted by the collection bounds above stays well under 400 KB, so this bounds
	// the pre-authentication parse cost while keeping a wide margin for any valid request.
	public const int MaxRequestSize = 2 * 1024 * 1024;

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

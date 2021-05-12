namespace WalletWasabi.WabiSabi
{
	public static class ProtocolConstants
	{
		public const int CredentialNumber = 2;
		public const ulong MaxAmountPerAlice = 4_300_000_000_000ul;
		public const uint MaxVsizePerAlice = 255;

		public const string WabiSabiProtocolIdentifier = "WabiSabi_v1.0";
		public const string DomainStrobeSeparator = "domain-separator";

		// Round hashing labels
		public const string RoundStrobeDomain = "round-parameters";
		public const string RoundMaxInputCountByAliceStrobeLabel = "maximum-input-count-by-alice";
		public const string RoundMinRegistrableAmountStrobeLabel = "minimum-registrable-amount";
		public const string RoundMaxRegistrableAmountStrobeLabel = "maximum-registrable-amount";
		public const string RoundPerAliceVsizeAllocationStrobeLabel = "per-alice-vsize-allocation";
		public const string RoundAmountCredentialIssuerParametersStrobeLabel = "amount-credential-issuer-parameters";
		public const string RoundVsizeCredentialIssuerParametersStrobeLabel = "vsize-credential-issuer-parameters";
		public const string RoundFeeRateStrobeLabel = "fee-rate";

		// Alice hashing labels
		public const string AliceStrobeDomain = "alice-parameters";
		public const string AliceCoinTxOutStrobeLabel = "coin-txout";
		public const string AliceCoinOutpointStrobeLabel = "coin-outpoint";
		public const string AliceOwnershipProofStrobeLabel = "ownership-proof";
		public const string ProtocolViolationType = "wabisabi-protocol-violation";
	}
}

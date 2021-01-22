namespace WalletWasabi.WabiSabi.Crypto
{
	public enum WabiSabiErrorCode
	{
		Unspecified = 0,
		SerialNumberAlreadyUsed = 1,
		CoordinatorReceivedInvalidProofs = 2,
		NegativeBalance = 3,
		InvalidBitCommitment = 4,
		ClientReceivedInvalidProofs = 5,
		IssuedCredentialNumberMismatch = 6,
		SerialNumberDuplicated = 7,
		NotEnoughZeroCredentialToFillTheRequest = 8,
		InvalidNumberOfRequestedCredentials = 9,
		InvalidNumberOfPresentedCredentials = 10,
		CredentialToPresentDuplicated = 11,
	}
}

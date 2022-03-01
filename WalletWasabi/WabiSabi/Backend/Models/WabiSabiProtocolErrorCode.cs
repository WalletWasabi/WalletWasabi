namespace WalletWasabi.WabiSabi.Backend.Models;

public enum WabiSabiProtocolErrorCode
{
	RoundNotFound,
	WrongPhase,
	InputSpent,
	InputUnconfirmed,
	InputImmature,
	WrongOwnershipProof,
	TooManyInputs,
	NotEnoughFunds,
	TooMuchFunds,
	NonUniqueInputs,
	InputBanned,
	InputNotWhitelisted,
	AliceNotFound,
	IncorrectRequestedVsizeCredentials,
	TooMuchVsize,
	ScriptNotAllowed,
	IncorrectRequestedAmountCredentials,
	WrongCoinjoinSignature,
	AliceAlreadyRegistered,
	NonStandardInput,
	NonStandardOutput,
	WitnessAlreadyProvided,
	InsufficientFees,
	SizeLimitExceeded,
	DustOutput,
	UneconomicalInput,
	VsizeQuotaExceeded,
	DeltaNotZero,
	WrongNumberOfCreds,
	CryptoException,
	AliceAlreadySignalled,
	AliceAlreadyConfirmedConnection,
	AlreadyRegisteredOutputScript
}

public static class WabiSabiProtocolErrorCodeExtension
{
	public static bool IsEvidencingClearMisbehavior(this WabiSabiProtocolErrorCode errorCode) =>
		errorCode
			is WabiSabiProtocolErrorCode.InputSpent
			or WabiSabiProtocolErrorCode.WrongOwnershipProof
			or WabiSabiProtocolErrorCode.ScriptNotAllowed
			or WabiSabiProtocolErrorCode.NonStandardInput
			or WabiSabiProtocolErrorCode.NonStandardOutput
			or WabiSabiProtocolErrorCode.DeltaNotZero
			or WabiSabiProtocolErrorCode.WrongNumberOfCreds
			or WabiSabiProtocolErrorCode.NonUniqueInputs
			or WabiSabiProtocolErrorCode.CryptoException;
}

namespace WalletWasabi.WabiSabi.Coordinator.Models;

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
	InputLongBanned,
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
	AlreadyRegisteredScript,
	SignatureTooLong,
}

public static class WabiSabiProtocolErrorCodeExtension
{
	public static bool IsEvidencingClearMisbehavior(this WabiSabiProtocolErrorCode errorCode) =>
		errorCode
			is WabiSabiProtocolErrorCode.ScriptNotAllowed
			or WabiSabiProtocolErrorCode.NonStandardInput
			or WabiSabiProtocolErrorCode.NonStandardOutput
			or WabiSabiProtocolErrorCode.DeltaNotZero
			or WabiSabiProtocolErrorCode.WrongNumberOfCreds
			or WabiSabiProtocolErrorCode.NonUniqueInputs
			or WabiSabiProtocolErrorCode.CryptoException
			or WabiSabiProtocolErrorCode.AliceAlreadyConfirmedConnection;
}

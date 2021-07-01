using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.WabiSabi.Backend.Models
{
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
		CryptoException
	}
}

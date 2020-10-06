using System;
using System.Runtime.Serialization;

namespace WalletWasabi.Crypto.Api
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
	}

	[Serializable]
	public class WabiSabiException : Exception
	{
		public WabiSabiException()
		{
		}

		public WabiSabiException(string? message) : base(message)
		{
		}

		public WabiSabiException(WabiSabiErrorCode errorCode, string? message = null)
			: base(message)
		{
			ErrorCode = errorCode;
		}

		public WabiSabiException(string? message, Exception? innerException) 
			: base(message, innerException)
		{
		}

		protected WabiSabiException(SerializationInfo info, StreamingContext context) 
			: base(info, context)
		{
		}

		public WabiSabiErrorCode ErrorCode { get; }
	}
}
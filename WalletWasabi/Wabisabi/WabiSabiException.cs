using System;
using System.Runtime.Serialization;

namespace WalletWasabi.Wabisabi
{
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
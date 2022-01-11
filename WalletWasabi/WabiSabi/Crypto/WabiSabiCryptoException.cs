using System.Runtime.Serialization;

namespace WalletWasabi.WabiSabi.Crypto;

[Serializable]
public class WabiSabiCryptoException : Exception
{
	public WabiSabiCryptoException(WabiSabiCryptoErrorCode errorCode, string? message = null, Exception? innerException = null)
		: base(message, innerException)
	{
		ErrorCode = errorCode;
	}

	protected WabiSabiCryptoException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}

	public WabiSabiCryptoErrorCode ErrorCode { get; }
}

using System.Linq;

namespace WalletWasabi.WabiSabi.Backend.Models;

public class WabiSabiProtocolException : Exception
{
	public WabiSabiProtocolException(WabiSabiProtocolErrorCode errorCode, string? message = null, Exception? innerException = null)
		: base(message ?? ErrorCodeDescription(errorCode), innerException)
	{
		ErrorCode = errorCode;
	}

	public WabiSabiProtocolErrorCode ErrorCode { get; }

	private static string ErrorCodeDescription(WabiSabiProtocolErrorCode errorCode)
	{
		var enumName = Enum.GetName<WabiSabiProtocolErrorCode>(errorCode) ?? "";
		var errorDescription = string.Join(
			"",
			enumName.Select((c, i) => i > 0 && char.IsUpper(c)
				? " " + char.ToLowerInvariant(c)
				: "" + c));
		return errorDescription;
	}
}

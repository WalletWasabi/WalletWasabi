using Newtonsoft.Json;
using System.Text;
using WalletWasabi.Serialization;

namespace WalletWasabi.Models;

public record SerializableException
{
	public SerializableException(string exceptionType, string message, string stackTrace, SerializableException? innerException)
	{
		ExceptionType = exceptionType;
		Message = message;
		StackTrace = stackTrace;
		InnerException = innerException;
	}

	public SerializableException(Exception ex)
	{
		if (ex.InnerException is { })
		{
			InnerException = new SerializableException(ex.InnerException);
		}

		ExceptionType = ex.GetType().FullName;

		Message = ex.Message;
		StackTrace = ex.StackTrace;
	}

	public string? ExceptionType { get; }

	public string Message { get; }

	public string? StackTrace { get; }

	public SerializableException? InnerException { get; }

	public static string ToBase64String(SerializableException exception)
	{
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonEncoder.ToString(exception, Encode.SerializableException)));
	}

	public static SerializableException FromBase64String(string base64String)
	{
		var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64String));
		return JsonDecoder.FromString(json, Decode.SerializableException);
	}

	public override string ToString()
	{
		return string.Join(
			Environment.NewLine + Environment.NewLine,
			$"Exception type: {ExceptionType}",
			$"Message: {Message}",
			$"Stack Trace: {StackTrace}",
			$"Inner Exception: {InnerException}");
	}
}

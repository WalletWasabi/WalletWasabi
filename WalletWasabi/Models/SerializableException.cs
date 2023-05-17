using Newtonsoft.Json;
using System.Text;

namespace WalletWasabi.Models;

[JsonObject(MemberSerialization.OptIn)]
public record SerializableException
{
	[JsonConstructor]
	protected SerializableException(string exceptionType, string message, string stackTrace, SerializableException innerException)
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

	[JsonProperty(PropertyName = "ExceptionType")]
	public string? ExceptionType { get; }

	[JsonProperty(PropertyName = "Message")]
	public string Message { get; }

	[JsonProperty(PropertyName = "StackTrace")]
	public string? StackTrace { get; }

	[JsonProperty(PropertyName = "InnerException")]
	public SerializableException? InnerException { get; }

	public static string ToBase64String(SerializableException exception)
	{
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(exception)));
	}

	public static SerializableException FromBase64String(string base64String)
	{
		var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64String));
		return JsonConvert.DeserializeObject<SerializableException>(json);
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

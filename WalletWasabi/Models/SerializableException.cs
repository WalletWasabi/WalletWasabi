using Newtonsoft.Json;
using System;
using System.Text;

namespace WalletWasabi.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public record SerializableException
	{
		public SerializableException()
		{
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
		public string? ExceptionType { get; private set; }

		[JsonProperty(PropertyName = "Message")]
		public string? Message { get; private set; }

		[JsonProperty(PropertyName = "StackTrace")]
		public string? StackTrace { get; private set; }

		[JsonProperty(PropertyName = "InnerException")]
		public SerializableException? InnerException { get; private set; }

		public static string ToBase64String(SerializableException exception)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(exception)));
		}

		public static SerializableException FromBase64String(string base64String)
		{
			var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64String));
			return JsonConvert.DeserializeObject<SerializableException>(json);
		}
	}
}

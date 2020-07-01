using Newtonsoft.Json;
using System;
using System.Text;

namespace WalletWasabi.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class SerializableException
	{
		public SerializableException()
		{
		}

		public SerializableException(Exception ex)
		{
			if (ex.InnerException != null)
			{
				InnerException = new SerializableException(ex.InnerException);
			}

			ExceptionType = ex.GetType().FullName;

			Message = ex.Message;
			StackTrace = ex.StackTrace;
		}

		[JsonProperty(PropertyName = "ExceptionType")]
		public string ExceptionType { get; set; }

		[JsonProperty(PropertyName = "Message")]
		public string Message { get; set; }

		[JsonProperty(PropertyName = "StackTrace")]
		public string StackTrace { get; set; }

		[JsonProperty(PropertyName = "InnerException")]
		public SerializableException InnerException { get; set; }

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

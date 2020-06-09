using Newtonsoft.Json;
using System;
using System.Text;

namespace WalletWasabi.Gui.CrashReport.Models
{
	[JsonObject(MemberSerialization.OptIn)]
	public class SerializedException
	{
		[JsonProperty(PropertyName = "ExceptionType")]
		public string ExceptionType { get; set; }

		[JsonProperty(PropertyName = "Message")]
		public string Message { get; set; }

		[JsonProperty(PropertyName = "StackTrace")]
		public string StackTrace { get; set; }

		[JsonProperty(PropertyName = "InnerException")]
		public SerializedException InnerException { get; set; }

		public SerializedException(Exception ex)
		{
			if (ex.InnerException != null)
			{
				InnerException = new SerializedException(ex.InnerException);
			}

			ExceptionType = ex.GetType().FullName;

			Message = ex.Message;
			StackTrace = ex.StackTrace;
		}

		public override string ToString()
		{
			int tabLevel = 0;

			var sb = new StringBuilder();

			if (!string.IsNullOrEmpty(ExceptionType))
			{
				sb.Append(Tabs(tabLevel));
				sb.Append("ExceptionType: ");
				sb.AppendLine(ExceptionType);
			}

			if (!string.IsNullOrEmpty(Message))
			{
				sb.Append(Tabs(tabLevel));
				sb.Append("Message: ");
				sb.AppendLine(Message);
			}

			if (!string.IsNullOrEmpty(StackTrace))
			{
				var header = "StackTrace: ";
				sb.Append(Tabs(tabLevel));
				sb.Append(header);
				sb.Append(Tabs(tabLevel));
				sb.AppendLine(MultiLineTabs(tabLevel, StackTrace, header.Length));
			}

			if (InnerException != null)
			{
				var header = "InnerException: ";
				sb.Append(Tabs(tabLevel));
				sb.Append(header);
				sb.Append(Tabs(tabLevel + 1));
				sb.AppendLine(MultiLineTabs(tabLevel + 1, InnerException.ToString(), header.Length));
			}

			return sb.ToString();
		}

		private string MultiLineTabs(int n, string t, int addedPadding = 0)
		{
			int i = 0;
			var sb = new StringBuilder();
			foreach (var line in t.Split(Environment.NewLine))
			{
				switch (i)
				{
					case 0:
						sb.AppendLine(line);
						i++;
						break;

					default:
						var padding = new string(' ', addedPadding);
						sb.AppendLine($"{padding}{Tabs(n)}{line}");
						break;
				}
			}
			return sb.ToString();
		}

		private string Tabs(int n)
		{
			return n == 0 ? string.Empty : new string(' ', n * 4);
		}

		public static string ToCommandLineArgument(Exception serializedException)
		{
			return JsonConvert.SerializeObject(new SerializedException(serializedException));
		}
	}
}

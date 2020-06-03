using System;
using System.Collections.Generic;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.CrashReporter.Models
{
	[Serializable]
	public class SerializedException
	{
		public string ExceptionType { get; set; }
		public string Message { get; set; }
		public string StackTrace { get; set; }
		public SerializedException InnerException { get; set; }

		public SerializedException()
		{
		}

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
			return ToStringCore();
		}

		internal string ToStringCore(int tabLevel = 0)
		{
			var sb = new System.Text.StringBuilder();

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

		internal string MultiLineTabs(int n, string t, int addedPadding = 0)
		{
			int i = 0;
			var sb = new System.Text.StringBuilder();
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

		internal string Tabs(int n)
		{
			return n == 0 ? string.Empty : new string(' ', n * 4);
		}
	}
}

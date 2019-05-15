using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
	public static class TextReaderExtensions
	{
		// Reads a line. If CRLF is false a line is defined as a sequence of characters followed by
		// a carriage return ('\r'), a line feed ('\n'), or a carriage return
		// immediately followed by a line feed. The resulting string does not
		// contain the terminating carriage return and/or line feed. The returned
		// value is null if the end of the input stream has been reached.
		// If CRLF is true, the line ends only at ("\r\n").
		public static string ReadLine(this TextReader me, bool strictCRLF = false)
		{
			if (strictCRLF == false)
			{
				return me.ReadLine();
			}

			var sb = new StringBuilder();
			while (true)
			{
				int ch = me.Read();
				if (ch == -1)
				{
					break;
				}

				if (ch == '\r' && me.Peek() == '\n')
				{
					me.Read();
					return sb.ToString();
				}
				sb.Append((char)ch);
			}
			if (sb.Length > 0)
			{
				return sb.ToString();
			}

			return null;
		}

		public static string ReadPart(this TextReader me, char separator)
		{
			var sb = new StringBuilder();
			while (true)
			{
				int ch = me.Read();
				if (ch == -1)
				{
					break;
				}

				if (ch == separator)
				{
					return sb.ToString();
				}
				sb.Append((char)ch);
			}
			if (sb.Length > 0)
			{
				return sb.ToString();
			}

			return null;
		}

		public static Task<string> ReadPartAsync(this TextReader me, char separator, CancellationToken ctsToken)
		{
			return Task<string>.Factory.StartNew(state =>
			{
				return ((TextReader)state).ReadPart(separator);
			},
			me, ctsToken, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
		}
	}
}

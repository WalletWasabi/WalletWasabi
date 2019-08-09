using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WalletWasabi.Gui.Helpers
{
	public static class PasswordHelper
	{
		public static string[] GetPossibleCompatiblityPasswords(string text)
		{
			List<string> possiblePasswords = new List<string>()
			{
				StringCutIssue(text)
			};

			return possiblePasswords.ToArray();
		}

		private static string StringCutIssue(string text)
		{
			// On OSX Avalonia gets the string from the Clipboard as byte[] and size.
			// The size was mistakenly taken from the size of the original string which is not correct because of the UTF8 encoding.

			byte[] bytes = Encoding.Default.GetBytes(text);
			var myString = Encoding.UTF8.GetString(bytes.Take(text.Length).ToArray());
			return text.Substring(0, myString.Length);
		}
	}
}

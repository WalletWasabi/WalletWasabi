using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Helpers
{
	public static class StringHelpers
	{
		public static string PascalCaseToPhrase(string str)
		{
			var pos = 0;
			var builder = new StringBuilder();
			foreach (var c in str)
			{
				if (char.IsUpper(c))
				{
					if (pos > 0)
					{
						builder.Append(" ");
					}

					builder.Append(char.ToLower(c));
				}
				else
				{
					builder.Append(c);
				}
				pos++;
			}
			return builder.ToString();
		}
	}
}
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace System.Text.Json
{
	public static class JsonHelpers
	{
		public static bool TryParseJToken(string text, out JToken token)
		{
			token = null;
			try
			{
				token = JToken.Parse(text);
				return true;
			}
			catch (JsonReaderException)
			{
				return false;
			}
		}

		public static T CreateObject<T>(this Utf8JsonReader reader, Func<string, T> stringConversion)
		{
			var value = Guard.Correct(reader.GetString());
			return value.Length == 0 ? default : stringConversion(value);
		}
	}
}

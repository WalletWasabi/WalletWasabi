using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Newtonsoft.Json
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
	}
}

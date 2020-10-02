using Newtonsoft.Json.Linq;

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

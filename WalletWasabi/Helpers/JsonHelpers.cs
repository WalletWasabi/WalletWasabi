using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Newtonsoft.Json;

public static class JsonHelpers
{
	public static bool TryParseJToken(string text, [NotNullWhen(true)] out JToken? token)
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

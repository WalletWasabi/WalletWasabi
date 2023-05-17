using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletWasabi.Helpers;

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

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Daemon;

public static class ArgumentHelpers
{
	public static bool TryGetValue<T>(string key, string[] args, Func<string, T> converter, [NotNullWhen(true)] out T? value)
	{
		var values = GetValues(key, args, converter);
		if (values.Length > 0)
		{
			value = values[0];
			return value != null;
		}

		value = default;
		return false;
	}

	public static T[] GetValues<T>(string key, string[] args, Func<string, T> converter)
	{
		var cliArgKey = "--" + key + "=";
		return args
			.Where(a => a.StartsWith(cliArgKey, StringComparison.InvariantCultureIgnoreCase))
			.Select(x => converter(x[cliArgKey.Length..]))
			.ToArray();
	}
}

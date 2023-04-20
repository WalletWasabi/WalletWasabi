using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Daemon;

public static class ArgumentHelpers
{
	public static bool TryGetValue<T>(string key, string[] args, Func<string, T> converter, [NotNullWhen(true)] out T? value)
	{
		var values = GetValues(key, args, converter);
		value = values.FirstOrDefault();
		return !Equals(value, default(T));
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

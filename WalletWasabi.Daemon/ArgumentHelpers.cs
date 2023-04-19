using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Daemon;

public static class ArgumentHelpers
{
	public static bool TryGetValue<T>(string key, string[] args, Func<string, T> converter, [NotNullWhen(true)] out T? value)
	{
		var cliArgKey = "--" + key + "=";
		var cliArgOrNull = args.FirstOrDefault(a => a.StartsWith(cliArgKey, StringComparison.InvariantCultureIgnoreCase));
		if (cliArgOrNull is { } cliArg)
		{
			value = converter(cliArg[cliArgKey.Length..]);
			return value != null;
		}

		value = default;
		return false;
	}
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Daemon;

public static class ArgumentHelpers
{
	public static bool TryGetValue(string key, string[] args, [NotNullWhen(true)] out string? value)
	{
		var values = GetValues(key, args);
		if (values.Length > 0)
		{
			value = values[0];
			return value != null;
		}

		value = default;
		return false;
	}

	public static string[] GetValues(string key, string[] args)
	{
		var cliArgKey = $"--{key}=";
		return args
			.Where(a => a.StartsWith(cliArgKey, StringComparison.InvariantCultureIgnoreCase))
			.ToArray();
	}
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace WalletWasabi.Daemon;

public static class ArgumentHelpers
{
	public static bool TryGetValue(string key, string[] args, [NotNullWhen(true)] out string? value)
	{
		string cliArgKey = $"--{key}=";

		foreach (string arg in args)
		{
			if (arg.StartsWith(cliArgKey, StringComparison.InvariantCultureIgnoreCase))
			{
				value = arg[cliArgKey.Length..];
				return true;
			}
		}

		value = null;
		return false;
	}

	public static string[] GetValues(string key, string[] args)
	{
		string cliArgKey = $"--{key}=";

		return args
			.Where(x => x.StartsWith(cliArgKey, StringComparison.InvariantCultureIgnoreCase))
			.Select(x => x[cliArgKey.Length..])
			.ToArray();
	}
}

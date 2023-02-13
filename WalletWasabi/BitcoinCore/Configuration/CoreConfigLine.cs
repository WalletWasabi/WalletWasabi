using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Configuration;

public class CoreConfigLine
{
	public CoreConfigLine(string line)
	{
		// The point of having the line would be to not modify the original config file, but oh well, nobody would be against by removing whitespaces.
		Line = Guard.Correct(line);
		if (Line.Length == 0 || Line.StartsWith('#'))
		{
			return;
		}

		var parts = Line.Split('=', StringSplitOptions.RemoveEmptyEntries).Select(x => Guard.Correct(x)).ToArray();

		if (parts.Length != 2 || parts.Any(x => x.Length == 0))
		{
			return;
		}

		Key = parts[0];
		Value = parts[1];
		Line = $"{Key} = {Value}";
	}

	public string Line { get; }
	public string Key { get; } = "";
	public string Value { get; } = "";
	public bool HasKeyValuePair => !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(Value);

	public override string ToString() => Line;
}

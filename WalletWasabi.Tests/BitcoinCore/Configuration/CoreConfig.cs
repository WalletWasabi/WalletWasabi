using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.BitcoinCore.Configuration;

public class CoreConfig
{
	public CoreConfig(CoreConfig coreConfig)
	{
		_lines = coreConfig._lines.ToList();
	}

	public CoreConfig()
	{
		_lines = new List<CoreConfigLine>();
	}

	private readonly List<CoreConfigLine> _lines;

	public int Count => _lines.Count;

	public IDictionary<string, string> ToDictionary()
	{
		var myDic = new Dictionary<string, string>();
		foreach (var line in _lines)
		{
			if (line.HasKeyValuePair)
			{
				myDic.Add(line.Key, line.Value);
			}
		}

		return myDic;
	}

	public override string ToString() => $"{Guard.Correct(string.Join(Environment.NewLine, _lines))}{Environment.NewLine}"; // Good practice to end with a newline.

	public bool AddOrUpdate(string configString)
	{
		var ret = false;
		foreach (var line in ParseConfigLines(configString))
		{
			if (line.HasKeyValuePair)
			{
				var foundLine = _lines.FirstOrDefault(x =>
						x.Key == line.Key
						|| line.Key == $"main.{x.Key}"
						|| x.Key == $"main.{line.Key}");

				if (foundLine is null)
				{
					_lines.Add(line);
					ret = true;
				}
				else if (foundLine.Value != line.Value)
				{
					_lines.Remove(foundLine);
					_lines.Add(line);
					ret = true;
				}
			}
			else
			{
				_lines.Add(line);
				ret = true;
			}
		}

		ret = RemoveEmptyDuplications() || ret;

		return ret;
	}

	private bool RemoveEmptyDuplications()
	{
		var ret = false;
		while (RemoveFirstEmptyDuplication())
		{
			ret = true;
		}
		return ret;
	}

	private bool RemoveFirstEmptyDuplication()
	{
		for (int i = 1; i < _lines.Count; i++)
		{
			CoreConfigLine currentLine = _lines[i];
			CoreConfigLine prevLine = _lines[i - 1];
			if (string.IsNullOrWhiteSpace(currentLine.Line) && string.IsNullOrWhiteSpace(prevLine.Line))
			{
				_lines.Remove(currentLine);
				return true;
			}
		}

		return false;
	}

	private static IEnumerable<CoreConfigLine> ParseConfigLines(string configString)
	{
		configString = Guard.Correct(configString);
		var allLines = configString.Split('\n', StringSplitOptions.None);
		var allCorrectedLines = allLines.Select(x => Guard.Correct(x))
			.Where((x, i) => i == 0 || x != allLines[i - 1]);

		var retLines = new List<string>();
		string? section = null;
		foreach (var line in allCorrectedLines)
		{
			if (line == "[main]")
			{
				section = "main.";
			}
			else if (line == "[test]")
			{
				section = "test.";
			}
			else if (line == "[regtest]")
			{
				section = "regtest.";
			}
			else
			{
				if (section is null
					|| line.StartsWith("main.", StringComparison.OrdinalIgnoreCase)
					|| line.StartsWith("test.", StringComparison.OrdinalIgnoreCase)
					|| line.StartsWith("regtest.", StringComparison.OrdinalIgnoreCase))
				{
					retLines.Add(line);
				}
				else
				{
					retLines.Add($"{section}{line}");
				}
			}
		}

		return retLines.Select(x => new CoreConfigLine(x));
	}

	public int RemoveAll(string configKey) => _lines.RemoveAll(line => line.Key.Contains(configKey));
}

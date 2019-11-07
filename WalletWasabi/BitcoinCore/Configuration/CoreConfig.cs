using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore.Configuration
{
	public class CoreConfig
	{
		private List<CoreConfigLine> Lines { get; }

		public int Count => Lines.Count;

		public CoreConfig(CoreConfig coreConfig)
		{
			Lines = coreConfig.Lines.ToList();
		}

		public CoreConfig()
		{
			Lines = new List<CoreConfigLine>();
		}

		public IDictionary<string, string> ToDictionary()
		{
			var myDic = new Dictionary<string, string>();
			foreach (var line in Lines)
			{
				if (line.HasKeyValuePair)
				{
					myDic.Add(line.Key, line.Value);
				}
			}

			return myDic;
		}

		public override string ToString() => $"{Guard.Correct(string.Join(Environment.NewLine, Lines))}{Environment.NewLine}"; // Good practice to end with a newline.

		/// <summary>
		/// TryAdd and AddOrUpdate are sisters. TryAdd always considers the first occurrence of a key as valid, while AddOrUpdate considers the last occurrence of it.
		/// </summary>
		public void TryAdd(string configString)
		{
			foreach (var line in ParseConfigLines(configString))
			{
				if (line.HasKeyValuePair)
				{
					var foundLine = Lines.FirstOrDefault(x => x.Key == line.Key);
					if (foundLine is null)
					{
						Lines.Add(line);
					}
				}
				else
				{
					Lines.Add(line);
				}
				RemoveFirstEmptyDuplication();
			}
		}

		/// <summary>
		/// TryAdd and AddOrUpdate are sisters. TryAdd always considers the first occurrence of a key as valid, while AddOrUpdate considers the last occurrence of it.
		/// </summary>
		public bool AddOrUpdate(string configString)
		{
			var ret = false;
			foreach (var line in ParseConfigLines(configString))
			{
				if (line.HasKeyValuePair)
				{
					var foundLine = Lines.FirstOrDefault(x => x.Key == line.Key);
					if (foundLine is null)
					{
						Lines.Add(line);
						ret = true;
					}
					else if (foundLine.Value != line.Value)
					{
						Lines.Remove(foundLine);
						Lines.Add(line);
						ret = true;
					}
				}
				else
				{
					Lines.Add(line);
					ret = true;
				}
				ret = ret || RemoveFirstEmptyDuplication();
			}

			return ret;
		}

		private bool RemoveFirstEmptyDuplication()
		{
			CoreConfigLine toRemove = null;
			for (int i = 1; i < Lines.Count; i++)
			{
				CoreConfigLine currentLine = Lines[i];
				CoreConfigLine prevLine = Lines[i - 1];
				if (string.IsNullOrWhiteSpace(currentLine.Line) && string.IsNullOrWhiteSpace(prevLine.Line))
				{
					toRemove = currentLine;
					break;
				}
			}

			if (toRemove != null)
			{
				Lines.Remove(toRemove);
				return true;
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
			string section = null;
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
			return retLines
				.Select(x => new CoreConfigLine(x));
		}
	}
}

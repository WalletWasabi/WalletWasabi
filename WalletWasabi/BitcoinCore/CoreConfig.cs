using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore
{
	public class CoreConfig
	{
		private List<CoreConfigLine> Lines { get; } = new List<CoreConfigLine>();

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
		public void AddOrUpdate(string configString)
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
					else
					{
						Lines.Remove(foundLine);
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

		private void RemoveFirstEmptyDuplication()
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
			}
		}

		private static IEnumerable<CoreConfigLine> ParseConfigLines(string configString)
		{
			configString = Guard.Correct(configString);
			var allLines = configString.Split('\n', StringSplitOptions.None);
			return allLines
				.Select(x => Guard.Correct(x))
				.Where((x, i) => i == 0 || x != allLines[i - 1])
				.Select(x => new CoreConfigLine(x));
		}
	}
}

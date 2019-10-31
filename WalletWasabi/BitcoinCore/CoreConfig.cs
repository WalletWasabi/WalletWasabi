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

		public override string ToString() => $"{string.Join(Environment.NewLine, Lines)}{Environment.NewLine}"; // Good practice to end with a newline.

		/// <summary>
		/// TryAdd and AddOrUpdate are sisters. TryAdd always considers the first occurrence of a key as valid, while AddOrUpdate considers the last occurrence of it.
		/// </summary>
		public void TryAdd(string configString)
		{
			configString = Guard.Correct(configString);
			foreach (var line in configString.Split(new char[] { '\r', '\n' }, StringSplitOptions.None))
			{
				var currentLine = new CoreConfigLine(line);
				if (currentLine.HasKeyValuePair)
				{
					var foundLine = Lines.FirstOrDefault(x => x.Key == currentLine.Key);
					if (foundLine is null)
					{
						Lines.Add(currentLine);
					}
				}
				else
				{
					Lines.Add(currentLine);
				}
			}
		}

		/// <summary>
		/// TryAdd and AddOrUpdate are sisters. TryAdd always considers the first occurrence of a key as valid, while AddOrUpdate considers the last occurrence of it.
		/// </summary>
		public void AddOrUpdate(string configString)
		{
			configString = Guard.Correct(configString);
			foreach (var line in configString.Split(new char[] { '\r', '\n' }, StringSplitOptions.None))
			{
				var currentLine = new CoreConfigLine(line);
				if (currentLine.HasKeyValuePair)
				{
					var foundLine = Lines.FirstOrDefault(x => x.Key == currentLine.Key);
					if (foundLine is null)
					{
						Lines.Add(currentLine);
					}
					else
					{
						Lines.Remove(foundLine);
						Lines.Add(currentLine);
					}
				}
				else
				{
					Lines.Add(currentLine);
				}
			}
		}
	}
}

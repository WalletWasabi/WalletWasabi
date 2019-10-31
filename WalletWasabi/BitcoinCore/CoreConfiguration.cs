using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore
{
	public class CoreConfiguration
	{
		private StringBuilder ConfigStringBuilder { get; } = new StringBuilder();

		public IDictionary<string, string> ToDictionary()
		{
			var myDic = new Dictionary<string, string>();
			foreach (var line in ConfigStringBuilder.ToString().Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => Guard.Correct(x)))
			{
				if (line.Length == 0 || line.StartsWith('#'))
				{
					continue;
				}

				var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries).Select(x => Guard.Correct(x)).ToArray();

				if (parts.Length != 2 || parts.Any(x => x.Length == 0))
				{
					continue;
				}

				if (!myDic.TryAdd(parts[0], parts[1]))
				{
					myDic.Remove(parts[0]);
					myDic.Add(parts[0], parts[1]);
				}
			}

			return myDic;
		}

		public void AddOrUpdate(string configString)
		{
			if (ConfigStringBuilder.Length != 0)
			{
				ConfigStringBuilder.Append(Environment.NewLine);
			}

			ConfigStringBuilder.Append(configString);
		}
	}
}

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.BitcoinCore
{
	public class NodeConfigParameters : Dictionary<string, string>
	{
		public void Import(NodeConfigParameters configParameters)
		{
			foreach (var kv in configParameters)
			{
				if (!ContainsKey(kv.Key))
				{
					Add(kv.Key, kv.Value);
				}
			}
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			foreach (var kv in this)
			{
				builder.AppendLine(kv.Key + "=" + kv.Value);
			}

			return builder.ToString();
		}

		public static async Task<NodeConfigParameters> LoadAsync(string configFile)
		{
			var config = new NodeConfigParameters();
			foreach (var line in await File.ReadAllLinesAsync(configFile))
			{
				var parts = line.Split('=');
				config.Add(parts[0], parts[1]);
			}
			return config;
		}
	}
}

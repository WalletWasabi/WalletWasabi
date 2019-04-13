using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WalletWasabi.Tests.NodeBuilding
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

		public static NodeConfigParameters Load(string configFile)
		{
			var config = new NodeConfigParameters();
			foreach (var line in File.ReadAllLines(configFile))
			{
				var parts = line.Split('=');
				config.Add(parts[0], parts[1]);
			}
			return config;
		}
	}
}

using System.Collections.Generic;
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
					Add(kv.Key, kv.Value);
			}
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			foreach (var kv in this)
				builder.AppendLine(kv.Key + "=" + kv.Value);
			return builder.ToString();
		}
	}
}

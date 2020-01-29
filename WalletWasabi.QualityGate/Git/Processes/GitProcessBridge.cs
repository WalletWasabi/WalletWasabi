using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Microservices;

namespace WalletWasabi.QualityGate.Git.Processes
{
	public class GitProcessBridge : ProcessBridge
	{
		public GitProcessBridge() : base(new BridgeConfiguration(processPath: null, processName: "git"))
		{
		}

		public async Task<int> GetNumberOfLinesChangedAsync()
		{
			var res = await SendCommandAsync("diff --numstat master", false, default).ConfigureAwait(false);
			var changes = res.response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			var totalChanges = 0;
			foreach (var change in changes)
			{
				var parts = change.Split('\t', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
				var added = int.Parse(parts[0]);
				var removed = int.Parse(parts[1]);
				totalChanges += added + removed;
			}

			return totalChanges;
		}
	}
}

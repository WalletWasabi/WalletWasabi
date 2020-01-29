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

		private async Task<string> SendCommandAsync(string command)
		{
			var res = await SendCommandAsync(command, false, default).ConfigureAwait(false);
			if (res.exitCode != 0)
			{
				throw new GitException($"git {command} did not exit cleanly. Exit code: {res.exitCode}.");
			}

			return res.response;
		}

		public async Task<int> GetNumberOfLinesChangedAsync()
		{
			var res = await SendCommandAsync("diff --numstat master").ConfigureAwait(false);
			var changes = res.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

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

		public async Task<string> GetCurrentCommitAsync()
		{
			return await SendCommandAsync("rev-parse HEAD").ConfigureAwait(false);
		}
	}
}

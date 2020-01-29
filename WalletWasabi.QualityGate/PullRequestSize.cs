using System;
using System.Threading.Tasks;
using WalletWasabi.QualityGate.Git.Processes;
using Xunit;

namespace WalletWasabi.QualityGate
{
	public class PullRequestSize
	{
		[Fact]
		public async Task PrTooLargeAsync()
		{
			var gitProcess = new GitProcessBridge();
			var res = await gitProcess.SendCommandAsync("status", false, default);
			Assert.NotNull(res.response);
		}
	}
}

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
			var git = new GitProcessBridge();
			var numberOfLinesChanged = await git.GetNumberOfLinesChangedAsync();
			Assert.InRange(numberOfLinesChanged, 0, 500);
		}
	}
}

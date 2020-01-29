using System;
using System.Diagnostics;
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
			var numberOfLinesChanged = await gitProcess.GetNumberOfLinesChangedAsync();
			Debug.WriteLine($"(debug) Number of lines changed: {numberOfLinesChanged}");
			Console.WriteLine($"(console) Number of lines changed: {numberOfLinesChanged}");
			Assert.InRange(numberOfLinesChanged, 0, 500);
		}
	}
}

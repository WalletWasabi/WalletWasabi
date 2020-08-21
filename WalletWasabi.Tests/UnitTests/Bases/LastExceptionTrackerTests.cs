using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Bases
{
	public class LastExceptionTrackerTests
	{
		[Fact]
		public void ProcessTest()
		{
			var let = new LastExceptionTracker();

			// No exception happened
			Assert.Empty(let.FinalizeExceptionsProcessing());
			Assert.Empty(let.FinalizeExceptionsProcessing());

			// Same exception encountered.
			let.Process(new InvalidOperationException());
			Assert.Matches("It came for [^ ]+ seconds, 1 times: InvalidOperationException", let.FinalizeExceptionsProcessing());

			// Same exception encountered.
			let.Process(new ArgumentOutOfRangeException());
			let.Process(new ArgumentOutOfRangeException());
			Assert.Matches("It came for [^ ]+ seconds, 2 times: ArgumentOutOfRangeException", let.FinalizeExceptionsProcessing());
			Assert.Empty(let.FinalizeExceptionsProcessing());
		}
	}
}

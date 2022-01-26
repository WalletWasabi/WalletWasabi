using WalletWasabi.Bases;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Bases;

public class LastExceptionTrackerTests
{
	[Fact]
	public void ProcessTest()
	{
		var let = new LastExceptionTracker();

		// Process first exception to process.
		{
			ExceptionInfo lastException = let.Process(new ArgumentOutOfRangeException());

			Assert.IsType<ArgumentOutOfRangeException>(lastException.Exception);
			Assert.Equal(1, lastException.ExceptionCount);
		}

		// Same exception encountered.
		{
			ExceptionInfo lastException = let.Process(new ArgumentOutOfRangeException());

			Assert.IsType<ArgumentOutOfRangeException>(lastException.Exception);
			Assert.Equal(2, lastException.ExceptionCount);
		}

		// Different exception encountered.
		{
			ExceptionInfo lastException = let.Process(new NotImplementedException());

			Assert.IsType<NotImplementedException>(lastException.Exception);
			Assert.Equal(1, lastException.ExceptionCount);
		}
	}
}

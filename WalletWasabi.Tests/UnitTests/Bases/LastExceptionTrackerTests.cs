#nullable enable

using System;
using WalletWasabi.Bases;
using Xunit;
using static WalletWasabi.Bases.LastExceptionTracker;

namespace WalletWasabi.Tests.UnitTests.Bases
{
	public class LastExceptionTrackerTests
	{
		[Fact]
		public void ProcessTest()
		{
			var let = new LastExceptionTracker();

			// No exception was processed at this point.
			Assert.Null(let.LastException);

			// Process first exception to process.
			{
				ExceptionInfo prevExceptionInfo = let.Process(new ArgumentOutOfRangeException())!;
				Assert.Null(prevExceptionInfo);

				Assert.NotNull(let.LastException);
				Assert.IsType<ArgumentOutOfRangeException>(let.LastException!.Exception);
				Assert.Equal(1, let.LastException!.ExceptionCount);
			}

			// Same exception encountered.
			{
				ExceptionInfo prevExceptionInfo = let.Process(new ArgumentOutOfRangeException())!;
				Assert.Null(prevExceptionInfo);

				Assert.NotNull(let.LastException);
				Assert.IsType<ArgumentOutOfRangeException>(let.LastException!.Exception);
				Assert.Equal(2, let.LastException!.ExceptionCount);
			}

			// Different exception encountered.
			{
				ExceptionInfo prevExceptionInfo = let.Process(new NotImplementedException())!;
				Assert.NotNull(prevExceptionInfo);
				Assert.IsType<ArgumentOutOfRangeException>(prevExceptionInfo.Exception);

				Assert.NotNull(let.LastException);
				Assert.IsType<NotImplementedException>(let.LastException!.Exception);
				Assert.Equal(1, let.LastException!.ExceptionCount);
			}
		}
	}
}

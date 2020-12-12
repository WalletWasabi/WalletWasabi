using System;
using WalletWasabi.Fluent.CrashReport;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class CrashReportTests
	{
		[Fact]
		public void CrashReporterCliArgTest()
		{
			CrashReporter crashing = new();

			Exception innerEx = new IndexOutOfRangeException("InnerMessage");
			Exception ex;
			try
			{
				// Throw it to have a stack-trace.
				throw new InvalidOperationException("Message", innerEx);
			}
			catch (Exception exc)
			{
				ex = exc;
			}

			crashing.SetException(ex);
			Assert.True(crashing.HadException);

			var args = crashing.ToCliArguments();

			CrashReporter reporting = new();

			// Args are split by space and passed to Main IRL.
			Assert.True(reporting.TryProcessCliArgs(args.Split(' ')));
			Assert.Equal(reporting.SerializedException, ex.ToSerializableException());
		}
	}
}

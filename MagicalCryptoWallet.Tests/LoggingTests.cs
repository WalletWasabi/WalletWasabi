using MagicalCryptoWallet.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class LoggingTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		public LoggingTests(SharedFixture fixture)
		{
			SharedFixture = fixture;
		}

		[Fact]
		public void LogFileDontGetFat()
		{
			var prevMaxSize = Logger.MaximumLogFileSize;
			var prevContained = Logger.Modes.Contains(LogMode.File);
			Logger.Modes.Add(LogMode.File);

			for (int i = 0; i < 50; i++)
			{
				Logger.LogCritical("trash");
			}

			var fi = new FileInfo(Logger.FilePath);
			Assert.True(fi.Length > 1000);

			Logger.SetMaximumLogFileSize(1);
			Logger.LogCritical("trash");
			fi = new FileInfo(Logger.FilePath);
			Assert.True(fi.Length <= 1000);

			Logger.SetMaximumLogFileSize(1);
			if (prevContained)
			{
				Logger.Modes.Remove(LogMode.File);
			}
		}
	}
}

using MagicalCryptoWallet.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace MagicalCryptoWallet.Tests
{
	public class SharedFixture : IDisposable
	{
		public SharedFixture()
		{
			// Initialize tests...

			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
			Logger.SetFilePath("TestLogs.txt");
		}

		public void Dispose()
		{
			// Cleanup tests...
		}
	}
}

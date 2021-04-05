using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor
{
	public class TorBinaryHashesTests
	{
		[Fact]
		public void VerifyTorBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "13b2a562aebb29307c23fa1eee1a22dd53b941d8919695d37771b37bd75d15bf" },
				{ OSPlatform.Linux, "621eb6ff196c9cd3262c3a13f8c9f2821f65083db53957bc993ae51bd3616865" },
				{ OSPlatform.OSX, "78e8e11873087cd13345b1cc2b7273286ba0960611c124beba98ae9f91a241aa" },
			};

			foreach ((OSPlatform platform, string expectedHash) in expectedHashes)
			{
				string filePath = TorSettings.GetTorBinaryFilePath(platform);

				using SHA256 sha256 = SHA256.Create();
				using FileStream fileStream = File.OpenRead(filePath);
				Assert.Equal(expectedHash, ByteHelpers.ToHex(sha256.ComputeHash(fileStream)).ToLowerInvariant());
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
	public class BitcoindBinaryHashesTests
	{
		[Fact]
		public void VerifyBitcoindBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "f3efa8047da261043b18cb360f1705a603393c691a8dc0a6c244ffb55f9a4b9d" },
				{ OSPlatform.Linux, "213fd04e89bf466092536554cf5475fdcc8498ccd57155d2dd965d0d64b4aa3c" },
				{ OSPlatform.OSX, "4efb294133f8fb7f54ecc380849a82fffea75ceb5a77d1dca3333f7a3df8709a" },
			};

			foreach (var item in expectedHashes)
			{
				string binaryFolder = MicroserviceHelpers.GetBinaryFolder(item.Key);
				string filePath = Path.Combine(binaryFolder, item.Key == OSPlatform.Windows ? "bitcoind.exe" : "bitcoind");

				using SHA256 sha256 = SHA256.Create();
				using FileStream fileStream = File.OpenRead(filePath);
				Assert.Equal(item.Value, ByteHelpers.ToHex(sha256.ComputeHash(fileStream)).ToLowerInvariant());
			}
		}
	}
}

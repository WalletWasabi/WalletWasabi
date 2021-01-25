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
		/// <summary>
		/// Bitcoin Knots distributes only SHA256 checksums for installers and for zip archives and not their content, so the validity of the hashes below depends on peer review consensus.
		/// </summary>
		/// <remarks>To verify a file hash, you can use, for example, <c>certUtil -hashfile bitcoind.exe SHA256</c> command.</remarks>
		/// <seealso href="https://bitcoinknots.org/files/0.20.x/0.20.0.knots20200614/SHA256SUMS.asc">Our current Bitcoin Knots version is 0.20.0.</seealso>
		[Fact]
		public void VerifyBitcoindBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "2406E8D63E8EE5DC71089049A52F5B3F3F7A39A9B054A323D7E3E0C8CBADEC1B" },
				{ OSPlatform.Linux, "09AFF9831D5EA24E69BBC27D16CF57C3763BA42DACD69F425710828E7F9101C8" },
				{ OSPlatform.OSX, "2353E36D938EB314ADE94DF2D18B85E23FD7232AC096FEAED46F0306C6B55B59" },
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

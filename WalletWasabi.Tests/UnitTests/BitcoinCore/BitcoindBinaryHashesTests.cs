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
		[Fact]
		public void VerifyBitcoindBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new Dictionary<OSPlatform, string>()
			{
				{ OSPlatform.Windows, "e51379c1950092514182494b9998152d9ad6d527358dd730945b2ea4e7ee5a51" },
				{ OSPlatform.Linux, "62d849c713c00821dbcca328651c287864c2d489aeafbd8a1a3f8b32870d4d8a" },
				{ OSPlatform.OSX, "e84db05bef03f62a7da36f1006c4734b9f417320f256cd67aa9bd1fcef5f87a5" },
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

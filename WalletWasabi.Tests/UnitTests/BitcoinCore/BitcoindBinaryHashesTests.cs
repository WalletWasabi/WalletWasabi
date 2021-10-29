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
				{ OSPlatform.Windows, "957d43d6290ad1bda3e4ee6dc1303a7dac56c378ad4a4b4896610fb02f4fe177" },
				{ OSPlatform.Linux, "72c713789869010a70f264a03261e7fcb9e2dc3052ba618f2e5056fb45cdc6e5" },
				{ OSPlatform.OSX, "182cd983fba123b77d6deaefee4669b9e256dda8274c86249e6f4b852dd02924" },
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

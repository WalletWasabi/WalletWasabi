using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Helpers;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

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

		Dictionary<OSPlatform, string> expectedHashes = new()
		{
			{ OSPlatform.Windows, "e047c18731d08bbcbc41e971f2de394958858123e8b454dbc3967cc45fce8532" },
			{ OSPlatform.Linux, "0beff6d2140f6e3163c0daf28d8a41e8c36ce39a2f467d80f0196beea0d0c230" },
			{ OSPlatform.OSX, "b16a2f80955b3ce3a2b831e21d5f8cc3758aef5414109f84d31bd0f08ca33888" },
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

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Helpers;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

public class HwiBinaryHashesTests
{
	/// <summary>
	/// Verifies HWI binaries distributed with Wasabi Wallet against checksums on https://github.com/bitcoin-core/HWI/releases/.
	/// </summary>
	/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.2.1/SHA256SUMS.txt.asc">Our current HWI version is 2.2.1.</seealso>
	[Fact]
	public void VerifyHwiBinaryChecksumHashes()
	{
		using var cts = new CancellationTokenSource(5_000);

		Dictionary<OSPlatform, string> expectedHashes = new()
		{
			{ OSPlatform.Windows, "460c8b83a9d8888ad769ffdc34dbe3ad7ecd27b22035494bdeb268d943be1791" },
			{ OSPlatform.Linux, "9519023b3a485b68668675db8ab70be2e338be100fd2731eeddd6d34fc440580" },
			{ OSPlatform.OSX, "9059b8f7cf6fe42f6e37cd8015cd11cb8fb736650797b25da849c625ed61ea62" },
		};

		foreach (var item in expectedHashes)
		{
			string binaryFolder = MicroserviceHelpers.GetBinaryFolder(item.Key);
			string filePath = Path.Combine(binaryFolder, item.Key == OSPlatform.Windows ? "hwi.exe" : "hwi");

			using SHA256 sha256 = SHA256.Create();
			using FileStream fileStream = File.OpenRead(filePath);
			Assert.Equal(item.Value, ByteHelpers.ToHex(sha256.ComputeHash(fileStream)).ToLowerInvariant());
		}
	}
}

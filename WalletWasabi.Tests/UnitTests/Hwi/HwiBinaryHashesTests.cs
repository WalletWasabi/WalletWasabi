using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi;

public class HwiBinaryHashesTests
{
	/// <summary>
	/// Verifies HWI binaries distributed with Wasabi Wallet against checksums on https://github.com/bitcoin-core/HWI/releases/.
	/// </summary>
	/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.0.2/SHA256SUMS.txt.asc">Our current HWI version is 2.0.2.</seealso>
	[Fact]
	public void VerifyHwiBinaryChecksumHashes()
	{
		using var cts = new CancellationTokenSource(5_000);

		Dictionary<OSPlatform, string> expectedHashes = new()
		{
			{ OSPlatform.Windows, "9e18cdb7c965541eb5d9a308c9360c3cd808ff3aa69bca740d172f73c6e797e7" },
			{ OSPlatform.Linux, "6bdcb40c3b653fdba20ed8847ee69994c1c2e9f4e1ad2a50fcc3ed53f2c5dd66" },
			{ OSPlatform.OSX, "7e7fbb907595e8718abe7aa297ee6a5d7fd0cfcd06a6913f51f7710df3a2c25e" },
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

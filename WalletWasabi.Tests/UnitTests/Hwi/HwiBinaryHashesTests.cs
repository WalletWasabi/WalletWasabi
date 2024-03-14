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
			{ OSPlatform.Windows, "2541b1e7ada2640e26ef7ce3491585e1788ad9be68d4ac5e818d38d23a4a9cab" },
			{ OSPlatform.Linux,   "15631dcc3020aa8d8a9b773744b61fd18f08ae862072068b795038d79f5b36b3" },
			{ OSPlatform.OSX,     "baa1c00c37e26590533e21f2daad01f4eba046aa5fbca1f82625914ce5241580" },
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

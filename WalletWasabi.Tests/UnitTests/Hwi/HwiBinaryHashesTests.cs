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
			{ OSPlatform.Windows, "38b3f02374c300516b4583a1195ffe1cac1159f9885b8ab434fd450e290c907a" },
			{ OSPlatform.Linux,   "9b70aab37a1265457de4aaa242bd24a0abef5056357d8337bd79232e9b85bc1c" },
			{ OSPlatform.OSX,     "d05c046d5718bf92b348a786aad15cb0f0132fcccf57a646758610240327a977" },
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

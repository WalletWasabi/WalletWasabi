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
	/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.0.2/SHA256SUMS.txt.asc">Our current HWI version is 2.0.2.</seealso>
	[Fact]
	public void VerifyHwiBinaryChecksumHashes()
	{
		using var cts = new CancellationTokenSource(5_000);

		Dictionary<OSPlatform, string> expectedHashes = new()
		{
			{ OSPlatform.Windows, "3efa5bcde386ca5523a4127f3a9802a7e9ef5320c2a8910ead343386c0b7dbfc" },
			{ OSPlatform.Linux, "7f4cbe4e5c2cd1ac892f9bd8ac35fb1f837b6a547b528b61aca895a212a90062" },
			{ OSPlatform.OSX, "1b1a903b4a9884aa06593356e7a958c19ccb56a5bc97e0c6075f968310640fd2" },
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Microservices;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Hwi
{
	public class HwiBinaryHashesTests
	{
		/// <summary>
		/// Verifies HWI binaries distributed with Wasabi Wallet against checksums on https://github.com/bitcoin-core/HWI/releases/.
		/// </summary>
		/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.0.0-rc.2/SHA256SUMS.txt.asc">Our current HWI version is 2.0.0.</seealso>
		[Fact]
		public void VerifyHwiBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "4137515eb653e22cfb36a40f57a7fba6ba67e68d8f9659b4c6f993cf22ab180e" },
				{ OSPlatform.Linux, "bea0f1e79210152e8233d4fa93a899e2f10633ee1ed3f7e96cc333f8c1ef9516" },
				{ OSPlatform.OSX, "c96ce39c6c793b232053886cc20c980c2d23df445efb6fb802e743e9005bb97f" },
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
}
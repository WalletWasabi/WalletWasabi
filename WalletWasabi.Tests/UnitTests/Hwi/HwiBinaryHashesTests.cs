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
		/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.0.1/SHA256SUMS.txt.asc">Our current HWI version is 2.0.1.</seealso>
		[Fact]
		public void VerifyHwiBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new Dictionary<OSPlatform, string>()
			{
				{ OSPlatform.Windows, "2cfdd6ae51e345f8c70214d626430c8d236336688a87f7d85fc6f3d6a8392da8" },
				{ OSPlatform.Linux, "ca1f91593b3c0a99269ecbc0f85aced08e2dec4bf263cfb25429e047e63e38d5" },
				{ OSPlatform.OSX, "389afc3927cbc6ce01f464d8d6fa66bf050d2b7d17d7127d1c1e6ee89c5b5ec1" },
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

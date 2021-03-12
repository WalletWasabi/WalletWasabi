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
		/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.0.0-rc.3/SHA256SUMS.txt.asc">Our current HWI version is 2.0.0.</seealso>
		[Fact]
		public void VerifyHwiBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "14c5148c4948eb8c4e74ab92664924b9363b80ff0334c67e5bb3139cc0235154" },
				{ OSPlatform.Linux, "80564b438207241ce7693ecd9f601453ef33cffdf5bc09fb7a31d3fdfbec6361" },
				{ OSPlatform.OSX, "0f4f5ec90ee87ee8a20813a22e040a0b77ed934c9d93f88d4d17d4413a1f9376" },
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
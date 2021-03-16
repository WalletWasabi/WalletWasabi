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
		/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.0.0/SHA256SUMS.txt.asc">Our current HWI version is 2.0.0.</seealso>
		[Fact]
		public void VerifyHwiBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "e680a8f44f7a53f1ee4eb81261f4fe4f817030ebfa63b2a16950e36e5219db75" },
				{ OSPlatform.Linux, "66c44787efa858938e902fba2e5110f327e36b24c61912e2f17918b7a2673a2f" },
				{ OSPlatform.OSX, "a1796cbb9e81712447acc277b2c0074ebe40e9743b0b104cfdfb8570d3745253" },
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
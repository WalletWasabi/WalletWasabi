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
		/// <seealso href="https://github.com/bitcoin-core/HWI/releases/download/2.0.2-rc.1/SHA256SUMS.txt.asc">Our current HWI version is 2.0.2-rc.1.</seealso>
		[Fact]
		public void VerifyHwiBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "3cbd5b284dfbee07a4a837ccdacb99260ffa4d50af937cf237a2103f2d83f04b" },
				{ OSPlatform.Linux, "ae7777df6fe387d4903821979c92344fdce54ea44fd2db09912f57454c8830ee" },
				{ OSPlatform.OSX, "a43e1b708bfaed16fb009fe013d50793df90410810410fe340ff34b261eab75f" },
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
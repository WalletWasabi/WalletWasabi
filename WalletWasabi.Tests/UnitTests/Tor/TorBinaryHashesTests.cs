using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor;

public class TorBinaryHashesTests
{
	[Fact]
	public void VerifyTorBinaryChecksumHashes()
	{
		using var cts = new CancellationTokenSource(5_000);

		Dictionary<OSPlatform, string> expectedHashes = new()
		{
			{ OSPlatform.Windows, "f6c8a3bd07b939e7c736a1e811df907611b763d0f8dbfafa721d8bf6ebfec691" },
			{ OSPlatform.Linux, "5d5f175bf154f5f4cd2c460b9d4ea80c8219d07441f626e13a4d01c1408a0c28" },
			{ OSPlatform.OSX, "52e7b209f4994018d6671749eee236475ed448163aef7e5d02456a2d3a9cf6da" },
		};

		foreach ((OSPlatform platform, string expectedHash) in expectedHashes)
		{
			string filePath = TorSettings.GetTorBinaryFilePath(platform);

			using SHA256 sha256 = SHA256.Create();
			using FileStream fileStream = File.OpenRead(filePath);
			Assert.Equal(expectedHash, ByteHelpers.ToHex(sha256.ComputeHash(fileStream)).ToLowerInvariant());
		}
	}
}

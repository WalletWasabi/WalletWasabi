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
		using CancellationTokenSource cts = new(5_000);

		Dictionary<OSPlatform, string> expectedHashes = new()
		{
			{ OSPlatform.Windows, "95a6e3df881724a1242c69b745f644dae47b934398a90392355e491837f2b725" },
			{ OSPlatform.Linux, "5f288d5c930ca022c1893c4934d93cd5071d08e33b82489bae67fb62fa522769" },
			{ OSPlatform.OSX, "f12cf9d88d7cfab920f1e6716ea94aa81db70ce00af404a4fedb4480e5f24a26" },
		};

		using SHA256 sha256 = SHA256.Create();

		foreach ((OSPlatform platform, string expectedHash) in expectedHashes)
		{
			string filePath = TorSettings.GetTorBinaryFilePath(platform);
			using FileStream fileStream = File.OpenRead(filePath);

			string actualHash = ByteHelpers.ToHex(sha256.ComputeHash(fileStream)).ToLowerInvariant();
			Assert.Equal(expectedHash, actualHash);
		}
	}
}

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WalletWasabi.Helpers;
using WalletWasabi.Microservices;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor;

public class TorBinaryHashesTests
{
	[Fact]
	public void VerifyTorBinaryChecksumHashes()
	{
		Dictionary<OSPlatform, string> expectedHashes = new()
		{
			{ OSPlatform.Windows, "33049016dd8985e97e69d89cad74b59b06488310c0be86d0f83b10ee096b7875" },
			{ OSPlatform.Linux, "8f5f89e8dec6f4fa095ee10a9b16904cdb8a3d3f109d3b2929ff960ca15846ba" },
			{ OSPlatform.OSX, "0360821eeb291e290f09966af87e7a1ceaba6d0a4b60e11c15249aeef288d49b" },
		};

		using SHA256 sha256 = SHA256.Create();

		Dictionary<OSPlatform, string> actualHashes = new(capacity: expectedHashes.Count);

		foreach ((OSPlatform platform, string expectedHash) in expectedHashes)
		{
			string torFolder = Path.Combine(MicroserviceHelpers.GetBinaryFolder(platform), "Tor");
			string filePath = TorSettings.GetTorBinaryFilePath(torFolder, platform);
			using FileStream fileStream = File.OpenRead(filePath);

			string actualHash = ByteHelpers.ToHex(sha256.ComputeHash(fileStream)).ToLowerInvariant();
			actualHashes.Add(platform, actualHash);
		}

		Assert.Equal(expectedHashes, actualHashes);
	}
}

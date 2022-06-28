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
			{ OSPlatform.Windows, "c25db825556a3f419d3b081688f17056371ffbdef8ee8dfdf9e2a85cc63ea570" },
			{ OSPlatform.Linux, "fcb8f554c171ba5e5d230f71e6dbbfc782598ab19f3b7f0e8e5be4e8879bd5c0" },
			{ OSPlatform.OSX, "ce1b321c8db9d252a48dd86f4cdc79d0970116c9d6bdae292d3e7c473aeace2a" },
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

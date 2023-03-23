using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using WalletWasabi.Helpers;
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
			{ OSPlatform.Windows, "46fe244b548265c78ab961e8f787bc8bf21edbcaaf175fa3b8be3137c6845a82" },
			{ OSPlatform.Linux, "b9b69006a3e85c69ff9fc1639e36542340f429f772e4eab1c000ed476509dbff" },
			{ OSPlatform.OSX, "38eefc4a30255b04e6c8290a958b08e9c1c9fc4dc801e5f4081d04fe057ded10" },
		};

		using SHA256 sha256 = SHA256.Create();

		Dictionary<OSPlatform, string> actualHashes = new(capacity: expectedHashes.Count);

		foreach ((OSPlatform platform, string expectedHash) in expectedHashes)
		{
			string filePath = TorSettings.GetTorBinaryFilePath(platform);
			using FileStream fileStream = File.OpenRead(filePath);

			string actualHash = ByteHelpers.ToHex(sha256.ComputeHash(fileStream)).ToLowerInvariant();
			actualHashes.Add(platform, actualHash);
		}

		Assert.Equal(expectedHashes, actualHashes);
	}
}

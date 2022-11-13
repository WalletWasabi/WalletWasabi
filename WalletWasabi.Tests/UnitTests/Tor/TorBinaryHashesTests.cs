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
			{ OSPlatform.Windows, "70bc89de20f5c0bba18bb2aa8b85d68f8f77da29b905f8813705a6dc43e4d6d5" },
			{ OSPlatform.Linux, "08ecedec71911f87d94428b9ea8da88ca893d9f2cb4530cd25b5d12c259c76e7" },
			{ OSPlatform.OSX, "b97b69ad0a38a53943f4ee2fc1c6ea2f7e1e87f14f13dc052611c7ca41e89a3f" },
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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Helpers;
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
			{ OSPlatform.Windows, "50e4a19d350a6c893932cc1aaca50fa9651f9f9a84b27aacb877c92a0d372daa" },
			{ OSPlatform.Linux, "6809d856cec215f4bad75973d91a4d7169810832f5fb0a22f6328a8eac058a79" },
			{ OSPlatform.OSX, "81c543e6fcdfcdd9463da04819a1967f42a3b14dc08a02671a24f0ea68572efd" },
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

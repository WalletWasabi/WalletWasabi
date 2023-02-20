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
			{ OSPlatform.Windows, "510a91b4ff9786b0a04e13cf6b2d1189d9282f740b2d7e4d731e1e69a599a47d" },
			{ OSPlatform.Linux, "1d274f8da7f7d5d33b36ec652c0143441020d524ba195d388eb5b49b943f05b6" },
			{ OSPlatform.OSX, "51ff9e0f35ae4153aa0008e3df60a9ed3f4c98e322711019c4d28d591fcf7f10" },
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

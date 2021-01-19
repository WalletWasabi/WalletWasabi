using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor
{
	public class TorBinaryHashesTests
	{
		[Fact]
		public void VerifyTorBinaryChecksumHashes()
		{
			using var cts = new CancellationTokenSource(5_000);

			Dictionary<OSPlatform, string> expectedHashes = new()
			{
				{ OSPlatform.Windows, "d244a89f7ca9da2259925affa0bd008d3cdea1d9dfd24cf53efffb7c1eadc169" },
				{ OSPlatform.Linux, "af7302d62fc1e47f79af8860541365f77547233404302a1e601e1f367e6e2888" },
				{ OSPlatform.OSX, "fe6d719e18bf3a963f0274de259b7e029f40e4fe778f4d170bba343eb491af00" },
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
}

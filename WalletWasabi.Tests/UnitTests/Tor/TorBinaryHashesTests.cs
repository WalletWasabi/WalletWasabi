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
				{ OSPlatform.Windows, "886960e5698cd275f81e2ad3b9654275172a30f6e9f16ddf09b6b96ad4b50403" },
				{ OSPlatform.Linux, "ed212d0d7dcfd3cd3736333f1c8932698db5a61e9989f1b7fd65ccc9c03ecd31" },
				{ OSPlatform.OSX, "5d91f26f547ec7c341bb9fae556c3af2582608f221b085c71a258d806fc2f085" },
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

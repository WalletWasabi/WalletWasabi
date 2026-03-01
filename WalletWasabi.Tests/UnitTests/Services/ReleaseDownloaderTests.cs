using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Services;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.WebClients;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class ReleaseDownloaderTests
{
	[Fact]
	public async Task OfficiallySupportedOSesAsync()
	{
		var (sha256sums, sha256sumsAsc, sha256sumsWasabiSig)  = CreateSha256SumsFiles();

		// Setup HTTP responses for mocked handler
		var httpClientFactory = MockHttpClientFactory.Create([
			() => HttpResponseMessageEx.Ok(sha256sums),
			() => HttpResponseMessageEx.Ok(sha256sumsAsc),
			() => HttpResponseMessageEx.Ok(sha256sumsWasabiSig),
			() => HttpResponseMessageEx.Ok("binary file"),
		]);

		var eventBus = new EventBus();
		var asyncDownloader = ReleaseDownloader.ForOfficiallySupportedOSes(httpClientFactory, eventBus);
		(string, Uri)[] assets =
		[
			("SHA256SUMS", new Uri("https://myserver.com/SHA256SUMS")),
			("SHA256SUMS.asc", new Uri("https://myserver.com/SHA256SUMS.asc")),
			("SHA256SUMS.wasabisig", new Uri("https://myserver.com/SHA256SUMS.wasabisig")),
			("Wasabi-2.5.1-linux-x64.tar.gz", new Uri("https://myserver.com/Wasabi-2.5.1-linux-x64.tar.gz")),
			("Wasabi-2.5.1.msi", new Uri("https://myserver.com/Wasabi-2.5.1.msi")),
		];

		var installerObstainedTask = new TaskCompletionSource<string>();
		using var _ = eventBus.Subscribe<NewSoftwareVersionInstallerAvailable>(e => installerObstainedTask.TrySetResult(e.InstallerPath));

		var releaseInfo = new ReleaseInfo(Version.Parse("2.5.1"), assets.ToImmutableDictionary(x => x.Item1, x => x.Item2));
		await asyncDownloader(releaseInfo, CancellationToken.None);

		// Wait for the installer. Throws TimeoutException in case the installer is not found
		var installerPath = await installerObstainedTask.Task.WaitAsync(TimeSpan.FromMilliseconds(100));
		var installerContent = File.ReadAllText(installerPath);
		Assert.Equal("binary file", installerContent);
	}

	private static (string, string, string)  CreateSha256SumsFiles()
	{
		var sha256sums =
			"""
			31943d8dc1d00045d7cafb8cc448f7213549b6d1a8dc5a1291f3ce3ba9995fd9  ./Wasabi-2.5.1-arm64.dmg
			9a3924b98ad3ce5e51d2c84a7129054c2523f39643a6ea27f8118511ecd4cdba  ./Wasabi-2.5.1-linux-x64.tar.gz
			fb8d1984bbbd37eb05738e1ae06417f3b700cbb34eac2f7633946d5de8715995  ./Wasabi-2.5.1-linux-x64.zip
			2b36b6b7747ffc5868e1c17ec2a5e3742c407963e3d7e4d705a2cf3d43aa8ce1  ./Wasabi-2.5.1-macOS-arm64.zip
			366c4571db5293a41354e6f10db81f8b3daff92dc8d2aec43b6d2b6c3c533558  ./Wasabi-2.5.1-macOS-x64.zip
			d1bc3f291481930faf15f41e9736a317214dcefd65aa4110fd2c85900f282968  ./Wasabi-2.5.1-win-x64.zip
			b3f0a5ba1f643b9707a65176139851162591381677c2c4e06d51f2a20f66a4ad  ./Wasabi-2.5.1.deb
			ea0a9db9b556dc4e3d915f900c6b48c3ede9eeb491c595785dda0182c2c0d42a  ./Wasabi-2.5.1.dmg
			eb992b8e66c86073f5034fbd6b48aeb12be1f77896568794f8fad74de678774e  ./Wasabi-2.5.1.msi
			""";

		var sha256sumsAsc =
			"""
			-----BEGIN PGP SIGNED MESSAGE-----
			Hash: SHA256

			31943d8dc1d00045d7cafb8cc448f7213549b6d1a8dc5a1291f3ce3ba9995fd9  ./Wasabi-2.5.1-arm64.dmg
			9a3924b98ad3ce5e51d2c84a7129054c2523f39643a6ea27f8118511ecd4cdba  ./Wasabi-2.5.1-linux-x64.tar.gz
			fb8d1984bbbd37eb05738e1ae06417f3b700cbb34eac2f7633946d5de8715995  ./Wasabi-2.5.1-linux-x64.zip
			2b36b6b7747ffc5868e1c17ec2a5e3742c407963e3d7e4d705a2cf3d43aa8ce1  ./Wasabi-2.5.1-macOS-arm64.zip
			366c4571db5293a41354e6f10db81f8b3daff92dc8d2aec43b6d2b6c3c533558  ./Wasabi-2.5.1-macOS-x64.zip
			d1bc3f291481930faf15f41e9736a317214dcefd65aa4110fd2c85900f282968  ./Wasabi-2.5.1-win-x64.zip
			b3f0a5ba1f643b9707a65176139851162591381677c2c4e06d51f2a20f66a4ad  ./Wasabi-2.5.1.deb
			ea0a9db9b556dc4e3d915f900c6b48c3ede9eeb491c595785dda0182c2c0d42a  ./Wasabi-2.5.1.dmg
			eb992b8e66c86073f5034fbd6b48aeb12be1f77896568794f8fad74de678774e  ./Wasabi-2.5.1.msi
			-----BEGIN PGP SIGNATURE-----
			-----END PGP SIGNATURE-----

			""";
		var sha256SumsWasabiSig = "MEQCICRVReWPrPldOxcDdD4k9k32zFRtzd17eEJRgGwvLgVpAiBnn8lu1IZQpNP1PcO6wIHf9nmXgTw8LRUdfCaZgKtuSg==";
		return (sha256sums, sha256sumsAsc, sha256SumsWasabiSig);
	}
}

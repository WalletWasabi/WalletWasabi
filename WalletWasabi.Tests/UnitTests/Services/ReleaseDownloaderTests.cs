using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.WebClients;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

public class ReleaseDownloaderTests
{
	[Fact]
	public async Task DownloadsAndVerifiesInstallerAsync()
	{
		var version = Version.Parse("2.5.1");
		var installerFileName = $"Wasabi-{version}-linux-x64.tar.gz";
		var installersDirectory = await Common.GetEmptyWorkDirAsync();
		var installDirectory = Path.Combine(installersDirectory, version.ToString());

		var (sha256sumsAsc, sha256sumsWasabiSig) = CreateSha256SumsFiles();

		// An earlier download that is no longer needed once the new one is verified.
		var oldInstallDirectory = Directory.CreateDirectory(Path.Combine(installersDirectory, "2.5.0"));
		File.WriteAllText(Path.Combine(oldInstallDirectory.FullName, "Wasabi-2.5.0-linux-x64.tar.gz"), "old");

		// Setup HTTP responses for mocked handler
		var httpClientFactory = MockHttpClientFactory.Create([
			() => HttpResponseMessageEx.Ok(sha256sumsAsc),
			() => HttpResponseMessageEx.Ok(sha256sumsWasabiSig),
			() => HttpResponseMessageEx.Ok("binary file"),
		]);

		var eventBus = new EventBus();
		var asyncDownloader = ReleaseDownloader.ForOfficiallySupportedOSes(
			httpClientFactory,
			eventBus,
			installersDirectory,
			_ => installerFileName);
		(string, Uri)[] assets =
		[
			("SHA256SUMS.asc", new Uri("https://myserver.com/SHA256SUMS.asc")),
			("SHA256SUMS.wasabisig", new Uri("https://myserver.com/SHA256SUMS.wasabisig")),
			(installerFileName, new Uri($"https://myserver.com/{installerFileName}")),
		];

		var installerObtainedTask = new TaskCompletionSource<NewSoftwareVersionInstallerAvailable>();
		using var _ = eventBus.Subscribe<NewSoftwareVersionInstallerAvailable>(e => installerObtainedTask.TrySetResult(e));

		var releaseInfo = new ReleaseInfo(version, assets.ToImmutableDictionary(x => x.Item1, x => x.Item2));
		await asyncDownloader(releaseInfo, CancellationToken.None);

		Assert.True(installerObtainedTask.Task.IsCompletedSuccessfully);
		var (installerPath, installerSha256) = await installerObtainedTask.Task;
		Assert.Equal("binary file", File.ReadAllText(installerPath));
		Assert.Equal(installDirectory, Path.GetDirectoryName(installerPath));
		Assert.False(oldInstallDirectory.Exists);
		if (!OperatingSystem.IsWindows())
		{
			Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(installDirectory));
		}

		// The installer is checked again right before it is launched; a file swapped in the meantime must be rejected.
		Assert.True(Installer.IsInstallerIntact(installerPath, installerSha256));
		File.WriteAllText(installerPath, "tampered");
		Assert.False(Installer.IsInstallerIntact(installerPath, installerSha256));
	}

	[Fact]
	public async Task RejectsAndDeletesDownloadWithBadSignatureAsync()
	{
		var version = Version.Parse("2.5.1");
		var installerFileName = $"Wasabi-{version}-linux-x64.tar.gz";
		var installersDirectory = await Common.GetEmptyWorkDirAsync();
		var installDirectory = Path.Combine(installersDirectory, version.ToString());

		var (sha256sumsAsc, sha256sumsWasabiSig) = CreateSha256SumsFiles();
		var httpClientFactory = MockHttpClientFactory.Create([
			() => HttpResponseMessageEx.Ok(sha256sumsAsc + "\n"),
			() => HttpResponseMessageEx.Ok(sha256sumsWasabiSig),
		]);

		var asyncDownloader = ReleaseDownloader.ForOfficiallySupportedOSes(httpClientFactory, new EventBus(), installersDirectory, _ => installerFileName);
		(string, Uri)[] assets =
		[
			("SHA256SUMS.asc", new Uri("https://myserver.com/SHA256SUMS.asc")),
			("SHA256SUMS.wasabisig", new Uri("https://myserver.com/SHA256SUMS.wasabisig")),
			(installerFileName, new Uri($"https://myserver.com/{installerFileName}")),
		];
		var releaseInfo = new ReleaseInfo(version, assets.ToImmutableDictionary(x => x.Item1, x => x.Item2));

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => asyncDownloader(releaseInfo, CancellationToken.None));
		Assert.Equal("Invalid wasabi signature.", ex.Message);

		// The rejected list is removed so that it is fetched again on the next check.
		Assert.False(File.Exists(Path.Combine(installDirectory, "SHA256SUMS.asc")));
		Assert.False(File.Exists(Path.Combine(installDirectory, "SHA256SUMS.wasabisig")));
	}

	[Theory]
	[InlineData(OS.Windows, Architecture.X64, false, "Wasabi-2.5.1.msi")]
	[InlineData(OS.OSX, Architecture.X64, false, "Wasabi-2.5.1.dmg")]
	[InlineData(OS.OSX, Architecture.Arm64, false, "Wasabi-2.5.1-arm64.dmg")]
	[InlineData(OS.Linux, Architecture.X64, true, "Wasabi-2.5.1.deb")]
	[InlineData(OS.Linux, Architecture.X64, false, "Wasabi-2.5.1-linux-x64.tar.gz")]
	public void SelectsInstallerName(OS platform, Architecture architecture, bool isDebianBased, string expected)
	{
		var result = ReleaseDownloader.GetInstallerName(
			Version.Parse("2.5.1"),
			platform,
			architecture,
			isDebianBased);

		Assert.Equal(expected, result);
	}

	private static (string, string) CreateSha256SumsFiles()
	{
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
		return (sha256sumsAsc, sha256SumsWasabiSig);
	}
}

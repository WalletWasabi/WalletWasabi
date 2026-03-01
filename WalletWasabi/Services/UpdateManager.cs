using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Crypto;
using NNostr.Client;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.WebClients;
using static WalletWasabi.Services.UpdateManager;

namespace WalletWasabi.Services;

// The Downloader
public delegate Task AsyncReleaseDownloader(ReleaseInfo releaseInfo, CancellationToken cancellationToken);

/// <summary>
/// Manages software updates by periodically checking for new releases via Nostr
/// </summary>
public static class UpdateManager
{
	public record UpdateMessage;

	public static MessageHandler<UpdateMessage, Unit> CreateUpdater(Func<INostrClient> nostrClientFactory,
		AsyncReleaseDownloader releaseDownloader, EventBus eventBus) =>
		(_, _, cancellationToken) => UpdateAsync(nostrClientFactory, releaseDownloader, eventBus, cancellationToken);

	private static async Task<Unit> UpdateAsync(Func<INostrClient> nostrClientFactory, AsyncReleaseDownloader releaseDownloader, EventBus eventBus, CancellationToken cancellationToken)
	{
		using var nostrClient = nostrClientFactory();
		using var wasabiNostrClient = new WasabiNostrClient(nostrClient);
		try
		{
			// Connect to Nostr relays and check for release version updates
			await wasabiNostrClient.ConnectAnsSubscribeAsync(cancellationToken).ConfigureAwait(false);
			await ProcessReleaseEventsAsync(wasabiNostrClient, releaseDownloader, eventBus, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			// Ensure we disconnect regardless of the outcome
			await wasabiNostrClient.DisconnectAsync(cancellationToken).ConfigureAwait(false);
		}

		return Unit.Instance;
	}

	private static async Task ProcessReleaseEventsAsync(WasabiNostrClient wasabiNostrClient, AsyncReleaseDownloader releaseDownloader, EventBus eventBus, CancellationToken cancellationToken)
	{
		using var sixtySeconds = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sixtySeconds.Token);

		try
		{
			// Read all the events as an array
			var releases = await wasabiNostrClient.EventsReader
				.ReadAllAsync(linkedCancellationTokenSource.Token)
				.ToArrayAsync(linkedCancellationTokenSource.Token)
				.ConfigureAwait(false);

			// Find release with version greater than current version
			var latestRelease = releases
				.Where(x => x.Version > Constants.ClientVersion)
				.MaxBy(x => x.Version);

			if(latestRelease is not null)
			{
				Logger.LogInfo($"New version found: {latestRelease.Version}");

				// Notify about new version via event bus
				var updateStatus = new UpdateStatus(ClientVersion: latestRelease.Version, ClientUpToDate: false, IsReadyToInstall: false);
				eventBus.Publish(new NewSoftwareVersionAvailable(updateStatus));

				// Download the new version
				await releaseDownloader(latestRelease, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) // Cancelled task is something we expect
		{
			Logger.LogInfo("No new Wasabi release was found.");
		}
	}

	public record UpdateStatus(bool ClientUpToDate, bool IsReadyToInstall, Version ClientVersion);
}



// Downloads and verifies new software releases
public static class ReleaseDownloader
{
	private static readonly UserAgentPicker UserAgentGetter = UserAgent.GenerateUserAgentPicker(false);

	public static AsyncReleaseDownloader ForOfficiallySupportedOSes(IHttpClientFactory httpClientFactory, EventBus eventBus) =>
		(releaseInfo, cancellationToken) => DownloadNewWasabiReleaseVersionAsync(httpClientFactory, eventBus, releaseInfo, cancellationToken);

	public static AsyncReleaseDownloader ForUnsupportedLinuxDistributions() =>
		(_, _) =>
		{
			Logger.LogInfo("For Linux, get the correct update manually.");
			return Task.CompletedTask;
		};

	public static AsyncReleaseDownloader AutoDownloadOff() =>
		(_, _) =>
		{
			Logger.LogInfo("Auto Download is turned off. Get the correct update manually.");
			return Task.CompletedTask;
		};

	// Downloads and verifies a new Wasabi release version
	private static async Task DownloadNewWasabiReleaseVersionAsync(IHttpClientFactory httpClientFactory, EventBus eventBus, ReleaseInfo releaseInfo, CancellationToken cancellationToken)
	{
		var installDirectory = GetInstallDirectory(releaseInfo);

		// Download signature files in parallel
		var sha256SumsTask    = DownloadFileAsync(releaseInfo.Assets["SHA256SUMS"]);
		var sha256SumsAscTask = DownloadFileAsync(releaseInfo.Assets["SHA256SUMS.asc"]);
		var sha256SumsWasTask = DownloadFileAsync(releaseInfo.Assets["SHA256SUMS.wasabisig"]);
		await Task.WhenAll(sha256SumsTask, sha256SumsAscTask, sha256SumsWasTask).ConfigureAwait(false);

		// Verify signatures
		await VerifySha256SumsFileAsync(sha256SumsAscTask.Result, sha256SumsWasTask.Result, cancellationToken).ConfigureAwait(false);

		Logger.LogInfo("Trying to download new version.");

		// Find appropriate installer for current platform
		var installerFileName = GetInstallerName(releaseInfo.Version);
		var installerUriResult = GetInstallerUri(installerFileName);
		if (!installerUriResult.IsOk)
		{
			Logger.LogError(installerUriResult.Error);
			installDirectory.Delete(true);
			return;
		}

		var installerUri = installerUriResult.Value;

		if (!installerUri.Scheme.StartsWith("http") || !installerUri.IsAbsoluteUri)
		{
			Logger.LogError($"Can't download installer file '{installerFileName}' from '{installerUri}'. Only absolute http url are supported.");
			installDirectory.Delete(true);
			return;
		}

		var installerFilePath = await DownloadFileAsync(installerUri).ConfigureAwait(false);

		Logger.LogInfo($"Installer downloaded to: {installerFilePath}");

		// Verify installer hash match the expected one
		var installerHash = await GetExpectedInstallerHashAsync().ConfigureAwait(false);

		await VerifyInstallerHashAsync(installerFilePath, installerHash, cancellationToken).ConfigureAwait(false);
		Logger.LogInfo("Installer verified successfully");

		// Notify UI that there is an installer ready.
		var updateStatus = new UpdateStatus(ClientVersion: releaseInfo.Version, ClientUpToDate: false, IsReadyToInstall: true);
		eventBus.Publish(new NewSoftwareVersionAvailable(updateStatus));

		// Set installer file path, so on exit we can launch the installer.
		eventBus.Publish(new NewSoftwareVersionInstallerAvailable(installerFilePath));
		return;

		Task<string> DownloadFileAsync(Uri uri)
		{
			var filePath = Path.Combine(installDirectory.FullName, uri.Segments[^1]);
			return File.Exists(filePath)
				? Task.FromResult(filePath)
				: DownloadAsync(httpClientFactory, uri, filePath, cancellationToken);
		}

		Result<Uri, string> GetInstallerUri(string filename) =>
			releaseInfo.Assets.TryGetValue(filename, out var uri)
			? uri
			: Result<Uri, string>.Fail($"There is no file '{filename}'.");

		async Task<string> GetExpectedInstallerHashAsync()
		{
			var lines = await File.ReadAllLinesAsync(sha256SumsTask.Result, cancellationToken).ConfigureAwait(false);
			var s = lines.Select(l => l.Split("  ./", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
				.Select(a => (Hash: a[0], FileName: a[1]))
				.FirstOrDefault(a => a.FileName == installerFileName)
				.Hash ?? throw new InvalidOperationException($"{installerFileName} was not found.");
			return s;
		}
	}

	private static DirectoryInfo GetInstallDirectory(ReleaseInfo releaseInfo)
	{
		var installDirectoryPath = Path.Combine(Path.GetTempPath(), $"wasabi-installer-{releaseInfo.Version}");
		var installDirectory = Directory.CreateDirectory(installDirectoryPath);
		return installDirectory;
	}

	private static async Task<string> DownloadAsync(IHttpClientFactory httpClientFactory, Uri uri, string filePath, CancellationToken cancellationToken)
	{
		File.Delete(filePath);
		var httpClient = httpClientFactory.CreateClient($"{uri.Host}-installers");
		httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgentGetter());
		using var request = new HttpRequestMessage(HttpMethod.Get, uri);
		var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		using var fileStream = new FileStream(filePath, FileMode.Create);
		await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
		return filePath;
	}

	private static async Task VerifySha256SumsFileAsync(string sha256SumsAscFilePath, string wasabiSignatureFilePath,
		CancellationToken cancellationToken)
	{
		// Read the content file
		byte[] bytes = await File.ReadAllBytesAsync(sha256SumsAscFilePath, cancellationToken).ConfigureAwait(false);
		var computedHash = new uint256(SHA256.HashData(bytes));

		// Read the signature file
		var signatureText = await File.ReadAllTextAsync(wasabiSignatureFilePath, cancellationToken).ConfigureAwait(false);
		var signatureBytes = Convert.FromBase64String(signatureText);

		var wasabiSignature = ECDSASignature.FromDER(signatureBytes);

		var pubKey = new PubKey(Constants.WasabiPubKey);

		if (!pubKey.Verify(computedHash, wasabiSignature))
		{
			throw new InvalidOperationException("Invalid wasabi signature.");
		}
	}

	private static async Task VerifyInstallerHashAsync(string installerFilePath, string expectedHash, CancellationToken cancellationToken)
	{
		var bytes1 = await File.ReadAllBytesAsync(installerFilePath, cancellationToken).ConfigureAwait(false);
		var computedHash = SHA256.HashData(bytes1);
		var downloadedHash = Convert.ToHexString(computedHash).ToLower();

		if (expectedHash != downloadedHash)
		{
			throw new InvalidOperationException("Downloaded file hash doesn't match expected hash.");
		}
	}

	private static string GetInstallerName(Version version) =>
		(PlatformInformation.GetOsPlatform(), RuntimeInformation.ProcessArchitecture) switch
		{
			(OS.Windows, _) => $"Wasabi-{version}.msi",
			(OS.OSX, Architecture.Arm64) => $"Wasabi-{version}-arm64.dmg",
			(OS.OSX, _) => $"Wasabi-{version}.dmg",
			(OS.Linux, _) when PlatformInformation.IsDebianBasedOS() => $"Wasabi-{version}.deb",
			(OS.Linux, Architecture.X64) => $"Wasabi-{version}-linux-x64.tar.gz",
			_ => throw new NotSupportedException($"Unsupported platform: '{RuntimeInformation.OSDescription}'.")
		};

}

public static class Installer
{
	public static void StartInstallingNewVersion(string installerPath)
	{
		try
		{
			ProcessStartInfo startInfo;
			if (!File.Exists(installerPath))
			{
				throw new FileNotFoundException(installerPath);
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				startInfo = ProcessStartInfoFactory.Make(installerPath, "", true);
			}
			else
			{
				startInfo = new()
				{
					FileName = installerPath,
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Normal
				};
			}

			using var p = Process.Start(startInfo);

			if (p is null)
			{
				throw new InvalidOperationException($"Can't start {nameof(p)} {startInfo.FileName}.");
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// For MacOS, you need to start the process twice, first start => permission denied
				// TODO: find out why and fix.

				p!.WaitForExit(5000);
				p.Start();
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to install latest release. File might be corrupted.", ex);
		}
	}
}

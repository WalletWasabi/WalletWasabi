using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NBitcoin.Crypto;
using NNostr.Client;
using WalletWasabi.BundledApps;
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
		AsyncReleaseDownloader releaseDownloader, EventBus eventBus, Version? currentVersion = null) =>
		(_, _, cancellationToken) => UpdateAsync(nostrClientFactory, releaseDownloader, eventBus, currentVersion ?? Constants.ClientVersion, cancellationToken);

	private static async Task<Unit> UpdateAsync(Func<INostrClient> nostrClientFactory, AsyncReleaseDownloader releaseDownloader, EventBus eventBus, Version currentVersion, CancellationToken cancellationToken)
	{
		using var nostrClient = nostrClientFactory();
		using var wasabiNostrClient = new WasabiNostrClient(nostrClient, Constants.WasabiTeamNostrPubKey);
		try
		{
			// Connect to Nostr relays and check for release version updates
			await wasabiNostrClient.ConnectAndSubscribeAsync(cancellationToken).ConfigureAwait(false);
			await ProcessReleaseEventsAsync(wasabiNostrClient, releaseDownloader, eventBus, currentVersion, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (AggregateException e)
		{
			Logger.LogWarning($"It was not possible to check for updates. {e.Message}");
		}
		finally
		{
			// Ensure we disconnect regardless of the outcome
			await wasabiNostrClient.DisconnectAsync(cancellationToken).ConfigureAwait(false);
		}

		return Unit.Instance;
	}

	private static async Task ProcessReleaseEventsAsync(WasabiNostrClient wasabiNostrClient, AsyncReleaseDownloader releaseDownloader, EventBus eventBus, Version currentVersion, CancellationToken cancellationToken)
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
				.Where(x => x.Version > currentVersion)
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
	private static readonly UserAgentPicker UserAgentGetter = UserAgent.GenerateUserAgentPicker();

	public static AsyncReleaseDownloader ForOfficiallySupportedOSes(IHttpClientFactory httpClientFactory, EventBus eventBus, string installersDirectory) =>
		ForOfficiallySupportedOSes(httpClientFactory, eventBus, installersDirectory, GetInstallerName);

	internal static AsyncReleaseDownloader ForOfficiallySupportedOSes(
		IHttpClientFactory httpClientFactory,
		EventBus eventBus,
		string installersDirectory,
		Func<Version, string> getInstallerName) =>
		(releaseInfo, cancellationToken) => DownloadNewWasabiReleaseVersionAsync(
			httpClientFactory,
			eventBus,
			releaseInfo,
			installersDirectory,
			getInstallerName(releaseInfo.Version),
			cancellationToken);

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

	private const string SignedSha256SumsFileName = "SHA256SUMS.asc";
	private const string WasabiSignatureFileName = "SHA256SUMS.wasabisig";

	// Downloads and verifies a new Wasabi release version
	private static async Task DownloadNewWasabiReleaseVersionAsync(
		IHttpClientFactory httpClientFactory,
		EventBus eventBus,
		ReleaseInfo releaseInfo,
		string installersDirectory,
		string installerFileName,
		CancellationToken cancellationToken)
	{
		foreach (var assetName in new[] { SignedSha256SumsFileName, WasabiSignatureFileName, installerFileName })
		{
			if (!releaseInfo.Assets.TryGetValue(assetName, out var uri))
			{
				Logger.LogError($"Release {releaseInfo.Version} has no '{assetName}' asset.");
				return;
			}

			if (uri.Scheme is not ("http" or "https"))
			{
				Logger.LogError($"Can't download '{assetName}' from '{uri}'. Only http urls are supported.");
				return;
			}
		}

		var installDirectory = GetInstallDirectory(installersDirectory, releaseInfo);

		// Download and check the signed hash list before downloading the installer. A list that fails the check is
		// removed so that it is fetched again next time.
		await Task.WhenAll(DownloadFileAsync(SignedSha256SumsFileName), DownloadFileAsync(WasabiSignatureFileName)).ConfigureAwait(false);
		string[] sha256Sums;
		try
		{
			sha256Sums = await ReadVerifiedSha256SumsAsync(installDirectory, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e) when (e is not OperationCanceledException)
		{
			TryDelete(Path.Combine(installDirectory, SignedSha256SumsFileName));
			TryDelete(Path.Combine(installDirectory, WasabiSignatureFileName));
			throw;
		}

		var expectedHash = GetExpectedHash(sha256Sums, installerFileName);

		Logger.LogInfo("Trying to download new version.");

		var installerFilePath = await DownloadFileAsync(installerFileName).ConfigureAwait(false);

		Logger.LogInfo($"Installer downloaded to: {installerFilePath}");

		if (await HashFileAsync(installerFilePath, cancellationToken).ConfigureAwait(false) != expectedHash)
		{
			throw new InvalidOperationException("Downloaded file hash doesn't match expected hash.");
		}

		Logger.LogInfo("Installer verified successfully");

		// Notify UI that there is an installer ready.
		var updateStatus = new UpdateStatus(ClientVersion: releaseInfo.Version, ClientUpToDate: false, IsReadyToInstall: true);
		eventBus.Publish(new NewSoftwareVersionAvailable(updateStatus));

		// Set installer file path and hash, so on exit we can check and launch the installer.
		eventBus.Publish(new NewSoftwareVersionInstallerAvailable(installerFilePath, expectedHash));

		// Only the verified installer is kept; earlier downloads are no longer needed.
		foreach (var otherDirectory in Directory.GetDirectories(installersDirectory).Where(d => d != installDirectory))
		{
			await IoHelpers.TryDeleteDirectoryAsync(otherDirectory).ConfigureAwait(false);
		}

		return;

		Task<string> DownloadFileAsync(string assetName)
		{
			var filePath = Path.Combine(installDirectory, assetName);
			return File.Exists(filePath)
				? Task.FromResult(filePath)
				: DownloadAsync(httpClientFactory, releaseInfo.Assets[assetName], filePath, cancellationToken);
		}
	}

	private static string GetInstallDirectory(string installersDirectory, ReleaseInfo releaseInfo)
	{
		var installDirectory = Path.Combine(installersDirectory, releaseInfo.Version.ToString());

		if (OperatingSystem.IsWindows())
		{
			// Windows inherits the per-user profile ACLs.
			Directory.CreateDirectory(installDirectory);
			return installDirectory;
		}

		// Owner-only on Unix: the installer runs on exit, long after it was verified, so nobody else may be able to
		// write here. New directories get the mode in one step; pre-existing ones are tightened explicitly.
		var ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
		Directory.CreateDirectory(installDirectory, ownerOnly);
		File.SetUnixFileMode(installersDirectory, ownerOnly);
		File.SetUnixFileMode(installDirectory, ownerOnly);
		return installDirectory;
	}

	/// <summary>Removes the installers of versions that are not newer than the running one.</summary>
	public static async Task DeleteObsoleteInstallersAsync(string installersDirectory, Version currentVersion)
	{
		if (!Directory.Exists(installersDirectory))
		{
			return;
		}

		foreach (var directory in Directory.GetDirectories(installersDirectory))
		{
			if (Version.TryParse(Path.GetFileName(directory), out var version)
				&& version <= currentVersion
				&& !await IoHelpers.TryDeleteDirectoryAsync(directory).ConfigureAwait(false))
			{
				Logger.LogWarning($"Could not delete the obsolete installer directory '{directory}'.");
			}
		}
	}

	private static async Task<string> DownloadAsync(IHttpClientFactory httpClientFactory, Uri uri, string filePath, CancellationToken cancellationToken)
	{
		// Download to a temporary name so an interrupted download is never taken for a complete file.
		var partialFilePath = filePath + ".part";
		try
		{
			var httpClient = httpClientFactory.CreateClient($"{uri.Host}-installers");
			httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgentGetter());
			using var request = new HttpRequestMessage(HttpMethod.Get, uri);
			using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();
			var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			using (var fileStream = new FileStream(partialFilePath, FileMode.Create))
			{
				await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
			}

			File.Move(partialFilePath, filePath, overwrite: true);
		}
		catch
		{
			TryDelete(partialFilePath);
			throw;
		}

		return filePath;
	}

	/// <summary>Returns the lines of SHA256SUMS.asc after checking the Wasabi signature over the same bytes.</summary>
	private static async Task<string[]> ReadVerifiedSha256SumsAsync(string installDirectory, CancellationToken cancellationToken)
	{
		byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(installDirectory, SignedSha256SumsFileName), cancellationToken).ConfigureAwait(false);
		var signatureText = await File.ReadAllTextAsync(Path.Combine(installDirectory, WasabiSignatureFileName), cancellationToken).ConfigureAwait(false);
		var wasabiSignature = ECDSASignature.FromDER(Convert.FromBase64String(signatureText));

		if (!new PubKey(Constants.WasabiPubKey).Verify(new uint256(SHA256.HashData(bytes)), wasabiSignature))
		{
			throw new InvalidOperationException("Invalid wasabi signature.");
		}

		return Encoding.UTF8.GetString(bytes).Split('\n');
	}

	private static string GetExpectedHash(string[] sha256Sums, string installerFileName) =>
		sha256Sums
			.Select(l => l.Split("  ./", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
			.Where(a => a.Length == 2)
			.FirstOrDefault(a => a[1] == installerFileName)?[0]
			?? throw new InvalidOperationException($"{installerFileName} was not found.");

	internal static async Task<string> HashFileAsync(string filePath, CancellationToken cancellationToken)
	{
		using var stream = File.OpenRead(filePath);
		return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
	}

	private static void TryDelete(string filePath)
	{
		try
		{
			File.Delete(filePath);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			// Best effort; the next download overwrites it anyway.
		}
	}

	private static string GetInstallerName(Version version) =>
		GetInstallerName(
			version,
			PlatformInformation.GetOsPlatform(),
			RuntimeInformation.ProcessArchitecture,
			PlatformInformation.IsDebianBasedOS());

	internal static string GetInstallerName(Version version, OS platform, Architecture architecture, bool isDebianBased) =>
		(platform, architecture, isDebianBased) switch
		{
			(OS.Windows, _, _) => $"Wasabi-{version}.msi",
			(OS.OSX, Architecture.Arm64, _) => $"Wasabi-{version}-arm64.dmg",
			(OS.OSX, _, _) => $"Wasabi-{version}.dmg",
			(OS.Linux, _, true) => $"Wasabi-{version}.deb",
			(OS.Linux, Architecture.X64, false) => $"Wasabi-{version}-linux-x64.tar.gz",
			_ => throw new NotSupportedException($"Unsupported platform: '{RuntimeInformation.OSDescription}'.")
		};

}

public static class Installer
{
	public static void StartInstallingNewVersion(string installerPath, string expectedSha256)
	{
		try
		{
			ProcessStartInfo startInfo;
			if (!File.Exists(installerPath))
			{
				throw new FileNotFoundException(installerPath);
			}

			// Verified at download time, but that may have been hours ago. Defence in depth: check again before running it.
			if (!IsInstallerIntact(installerPath, expectedSha256))
			{
				Logger.LogError($"Installer '{installerPath}' no longer matches the hash it was verified with and will not be run. Deleting it.");
				IoHelpers.TryDeleteDirectoryAsync(Path.GetDirectoryName(installerPath)!).GetAwaiter().GetResult();
				return;
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				startInfo = ProcessStartInfoFactory.Make(installerPath, [], true);
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
				p.WaitForExit(5000);
				p.Start();
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to install latest release. File might be corrupted.", ex);
		}
	}

	public static bool IsInstallerIntact(string installerPath, string expectedSha256) =>
		ReleaseDownloader.HashFileAsync(installerPath, CancellationToken.None).GetAwaiter().GetResult() == expectedSha256;
}

using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Tor.Http;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Services;

public class UpdateManager : PeriodicRunner
{
	private const string ReleaseURL = "https://api.github.com/repos/WalletWasabi/WalletWasabi/releases/latest";

	public UpdateManager(TimeSpan period, string dataDir, bool downloadNewVersion, HttpClient githubHttpClient)
		: base(period)
	{
		InstallerDir = Path.Combine(dataDir, "Installer");
		GithubHttpClient = githubHttpClient;
		// The feature is disabled on linux at the moment because we install Wasabi Wallet as a Debian package.
		DownloadNewVersion = downloadNewVersion && (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
	}

	public event EventHandler<UpdateStatus>? UpdateAvailableToGet;

	private string InstallerPath { get; set; } = "";

	private string InstallerDir { get; }
	private HttpClient GithubHttpClient { get; }

	/// <summary>Whether to download the new installer in the background or not.</summary>
	private bool DownloadNewVersion { get; }

	/// <summary>Install new version on shutdown or not.</summary>
	public bool DoUpdateOnClose { get; set; }

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		try
		{
			var result = await GetLatestReleaseFromGithubAsync(cancellationToken).ConfigureAwait(false);
			Version availableMajorVersion = new(result.LatestClientVersion.Major,  result.LatestClientVersion.Minor,  result.LatestClientVersion.Build);

			bool updateAvailable = Helpers.Constants.ClientVersion < availableMajorVersion;

			if (!updateAvailable)
			{
				// After updating Wasabi, remove old installer file.
				Cleanup();
				return;
			}

			UpdateStatus updateStatus = new()
				{ClientVersion = availableMajorVersion, ClientUpToDate = !updateAvailable, IsReadyToInstall = false};

			if (DownloadNewVersion)
			{
				(result.InstallerDownloadUrl, result.InstallerFileName) = GetAssetToDownload(result.AssetDownloadLinks);
				(string installerPath, Version newVersion) = await GetInstallerAsync(result, cancellationToken).ConfigureAwait(false);
				InstallerPath = installerPath;
				Logger.LogInfo($"Version {newVersion} downloaded successfully.");
				updateStatus.IsReadyToInstall = true;
				updateStatus.ClientVersion = newVersion;
				updateStatus.ClientUpToDate = false;
			}

			UpdateAvailableToGet?.Invoke(this, updateStatus);
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogTrace("Getting new update was canceled.", ex);
		}
		catch (InvalidOperationException ex)
		{
			Logger.LogError("Getting new update failed with error.", ex);
			Cleanup();
		}
		catch (InvalidDataException ex)
		{
			Logger.LogWarning(ex);
		}
		catch (Exception ex)
		{
			Logger.LogError("Getting new update failed with error.", ex);
		}
	}

	/// <summary>
	/// Get or download installer for the newest release.
	/// </summary>
	private async Task<(string filePath, Version newVersion)> GetInstallerAsync(ReleaseInfo info, CancellationToken cancellationToken)
	{
		var sha256SumsFilePath = Path.Combine(InstallerDir, "SHA256SUMS.asc");

		// This will throw InvalidOperationException in case of invalid signature.
		await DownloadAndValidateWasabiSignatureAsync(sha256SumsFilePath, info.AssetDownloadLinks, cancellationToken).ConfigureAwait(false);

		var installerFilePath = Path.Combine(InstallerDir, info.InstallerFileName);

		try
		{
			if (!File.Exists(installerFilePath))
			{
				EnsureToRemoveCorruptedFiles();

				Logger.LogInfo($"Trying to download new version: {info.LatestClientVersion}");

				// Get file stream and copy it to downloads folder to access.
				using HttpRequestMessage request = new(HttpMethod.Get, info.InstallerDownloadUrl);
				using HttpResponseMessage response = await GithubHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
				byte[] installerFileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

				Logger.LogInfo("Installer downloaded, copying...");

				using MemoryStream stream = new(installerFileBytes);
				await CopyStreamContentToFileAsync(stream, installerFilePath, cancellationToken).ConfigureAwait(false);
			}
			string expectedHash = await GetHashFromSha256SumsFileAsync(info.InstallerFileName, sha256SumsFilePath).ConfigureAwait(false);
			await VerifyInstallerHashAsync(installerFilePath, expectedHash, cancellationToken).ConfigureAwait(false);
		}
		catch (IOException)
		{
			cancellationToken.ThrowIfCancellationRequested();
			throw;
		}

		return (installerFilePath, info.LatestClientVersion);
	}

	private async Task VerifyInstallerHashAsync(string installerFilePath, string expectedHash, CancellationToken cancellationToken)
	{
		var bytes = await WasabiSignerHelpers.GetShaComputedBytesOfFileAsync(installerFilePath, cancellationToken).ConfigureAwait(false);
		string downloadedHash = Convert.ToHexString(bytes).ToLower();

		if (expectedHash != downloadedHash)
		{
			throw new InvalidOperationException("Downloaded file hash doesn't match expected hash.");
		}
	}

	private async Task<string> GetHashFromSha256SumsFileAsync(string installerFileName, string sha256SumsFilePath)
	{
		string[] lines = await File.ReadAllLinesAsync(sha256SumsFilePath).ConfigureAwait(false);
		var correctLine = lines.FirstOrDefault(line => line.Contains(installerFileName))
			?? throw new InvalidOperationException($"{installerFileName} was not found.");
		return correctLine.Split(" ")[0];
	}

	private async Task CopyStreamContentToFileAsync(Stream stream, string filePath, CancellationToken cancellationToken)
	{
		if (File.Exists(filePath))
		{
			return;
		}
		var tmpFilePath = $"{filePath}.tmp";
		IoHelpers.EnsureContainingDirectoryExists(tmpFilePath);
		using (var file = File.OpenWrite(tmpFilePath))
		{
			await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

			// Closing the file to rename.
			file.Close();
		}
		File.Move(tmpFilePath, filePath);
	}

	private async Task<ReleaseInfo> GetLatestReleaseFromGithubAsync(CancellationToken cancellationToken)
	{
		using HttpRequestMessage message = new(HttpMethod.Get, ReleaseURL);
		message.Headers.UserAgent.Add(new("WalletWasabi", "2.0"));
		var response = await GithubHttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

		JObject jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

		string softwareVersion = jsonResponse["tag_name"]?.ToString() ?? throw new InvalidDataException($"Endpoint gave back wrong json data or it's changed.\n{jsonResponse}");

		// Make sure there are no non-numeric characters (besides '.') in the version string.
		softwareVersion = string.Concat(softwareVersion.Where(c => char.IsDigit(c) || c == '.').ToArray());

		Version githubVersion = new(softwareVersion);

		// Get all asset names and download URLs to find the correct one.
		List<JToken> assetsInfo = jsonResponse["assets"]?.Children().ToList() ?? throw new InvalidDataException("Missing assets from response.");
		List<string> assetDownloadLinks = new();
		foreach (JToken asset in assetsInfo)
		{
			assetDownloadLinks.Add(asset["browser_download_url"]?.ToString() ?? throw new InvalidDataException("Missing download url from response."));
		}

		return new ReleaseInfo(githubVersion, assetDownloadLinks);
	}

	private async Task DownloadAndValidateWasabiSignatureAsync(string sha256SumsFilePath, List<string> assetDownloadLinks, CancellationToken cancellationToken)
	{
		var wasabiSigFilePath = Path.Combine(InstallerDir, "SHA256SUMS.wasabisig");
		string sha256SumsUrl = assetDownloadLinks.First(url => url.Contains("SHA256SUMS.asc"));
		string wasabiSigUrl = assetDownloadLinks.First(url => url.Contains("SHA256SUMS.wasabisig"));

		try
		{
			using HttpRequestMessage sha256Request = new(HttpMethod.Get, sha256SumsUrl);
			using HttpResponseMessage sha256Response = await GithubHttpClient.SendAsync(sha256Request, cancellationToken).ConfigureAwait(false);
			string sha256Content = await sha256Response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

			IoHelpers.EnsureContainingDirectoryExists(sha256SumsFilePath);
			File.WriteAllText(sha256SumsFilePath, sha256Content);

			using HttpRequestMessage signatureRequest = new(HttpMethod.Get, wasabiSigUrl);
			using HttpResponseMessage signatureResponse = await GithubHttpClient.SendAsync(signatureRequest, cancellationToken).ConfigureAwait(false);
			string signatureContent = await signatureResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

			IoHelpers.EnsureContainingDirectoryExists(wasabiSigFilePath);
			File.WriteAllText(wasabiSigFilePath, signatureContent);

			await WasabiSignerHelpers.VerifySha256SumsFileAsync(sha256SumsFilePath).ConfigureAwait(false);
		}
		catch (HttpRequestException ex)
		{
			string message = "";
			if (ex.StatusCode is HttpStatusCode.NotFound)
			{
				message = "Wasabi signature files were not found under the API.";
			}
			else
			{
				message = "Something went wrong while getting Wasabi signature files.";
			}
			throw new InvalidOperationException(message, ex);
		}
		catch (IOException)
		{
			// There's a chance to get IOException when closing Wasabi during stream copying. Throw OperationCancelledException instead.
			cancellationToken.ThrowIfCancellationRequested();
			throw;
		}
	}

	private (string url, string fileName) GetAssetToDownload(List<string> assetDownloadLinks)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var url = assetDownloadLinks.First(url => url.Contains(".msi"));
			return (url, url.Split("/").Last());
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			var cpu = RuntimeInformation.ProcessArchitecture;
			if (cpu.ToString() == "Arm64")
			{
				var arm64url = assetDownloadLinks.First(url => url.Contains("arm64.dmg"));
				return (arm64url, arm64url.Split("/").Last());
			}
			var url = assetDownloadLinks.First(url => url.Contains(".dmg") && !url.Contains("arm64"));
			return (url, url.Split("/").Last());
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			throw new InvalidOperationException("For Linux, get the correct update manually.");
		}
		else
		{
			throw new InvalidOperationException("OS not recognized, download manually.");
		}
	}

	private void EnsureToRemoveCorruptedFiles()
	{
		DirectoryInfo folder = new(InstallerDir);
		if (folder.Exists)
		{
			IEnumerable<FileSystemInfo> corruptedFiles = folder.GetFileSystemInfos().Where(file => file.Extension.Equals(".tmp"));
			foreach (var file in corruptedFiles)
			{
				File.Delete(file.FullName);
			}
		}
	}

	private void Cleanup()
	{
		try
		{
			var folder = new DirectoryInfo(InstallerDir);
			if (folder.Exists)
			{
				Directory.Delete(InstallerDir, true);
			}
		}
		catch (Exception exc)
		{
			Logger.LogError("Failed to delete installer directory.", exc);
		}
	}

	public void StartInstallingNewVersion()
	{
		try
		{
			ProcessStartInfo startInfo;
			if (!File.Exists(InstallerPath))
			{
				throw new FileNotFoundException(InstallerPath);
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				startInfo = ProcessStartInfoFactory.Make(InstallerPath, "", true);
			}
			else
			{
				startInfo = new()
				{
					FileName = InstallerPath,
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Normal
				};
			}

			using Process? p = Process.Start(startInfo);

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

	private record ReleaseInfo(Version LatestClientVersion, List<string> AssetDownloadLinks)
	{
		public string InstallerDownloadUrl { get; set; } = "";
		public string InstallerFileName { get; set; } = "";
	}

	public record UpdateStatus
	{
		public bool ClientUpToDate { get; set; }
		public bool IsReadyToInstall { get; set; }
		public Version ClientVersion { get; set; } = new(0, 0, 0);
	}
}

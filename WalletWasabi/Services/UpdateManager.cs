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
using WalletWasabi.WebClients;
using static WalletWasabi.WebClients.WasabiNostrClient;

namespace WalletWasabi.Services;

public class UpdateManager : PeriodicRunner
{
	private const string ReleaseURL = "https://api.github.com/repos/WalletWasabi/WalletWasabi/releases/latest";

	public UpdateManager(TimeSpan period, string dataDir, bool downloadNewVersion, HttpClient githubHttpClient, EventBus eventBus, WasabiNostrClient nostrClient)
		: base(period)
	{
		_installerDir = Path.Combine(dataDir, "Installer");
		_githubHttpClient = githubHttpClient;
		_eventBus = eventBus;
		_nostrClient = nostrClient;

		// The feature is disabled on linux at the moment because we install Wasabi Wallet as a Debian package.
		_downloadNewVersion = downloadNewVersion && (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
	}

	private string InstallerPath { get; set; } = "";

	private readonly string _installerDir;
	private readonly HttpClient _githubHttpClient;
	private readonly EventBus _eventBus;
	private readonly WasabiNostrClient _nostrClient;

	/// <summary>Whether to download the new installer in the background or not.</summary>
	private readonly bool _downloadNewVersion;

	/// <summary>Install new version on shutdown or not.</summary>
	public bool DoUpdateOnClose { get; set; }

	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		try
		{
			while (await _nostrClient.UpdateChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
			{
				NostrUpdateAssets nostrUpdateAssets = await _nostrClient.UpdateChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
				Version availableVersion = new(nostrUpdateAssets.Version.Major, nostrUpdateAssets.Version.Minor, nostrUpdateAssets.Version.Build);

				bool updateAvailable = Helpers.Constants.ClientVersion < availableVersion;
				if (Helpers.Constants.ClientVersion == availableVersion || !updateAvailable)
				{
					// After updating Wasabi, remove old installer file.
					Cleanup();
					return;
				}

				UpdateStatus updateStatus = new()
				{ ClientVersion = availableVersion, ClientUpToDate = !updateAvailable, IsReadyToInstall = false };

				if (_downloadNewVersion)
				{
					(Asset asset,string filename, Uri sha256sumsUrl, Uri wasabiSigUrl) result = GetAssetToDownload(nostrUpdateAssets.Assets);
					string installerPath = await GetInstallerAsync(result, cancellationToken).ConfigureAwait(false);
					InstallerPath = installerPath;
					Logger.LogInfo($"Version {availableVersion} downloaded successfully.");
					updateStatus.IsReadyToInstall = true;
					updateStatus.ClientVersion = availableVersion;
					updateStatus.ClientUpToDate = false;
				}
				_eventBus.Publish(new NewSoftwareVersionAvailable(updateStatus));
			}		
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogTrace("Getting new update was canceled.", ex);
			Cleanup();
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
	private async Task<string> GetInstallerAsync((Asset Asset, string FileName, Uri sha256sumsUrl, Uri wasabiSigUrl) asset, CancellationToken cancellationToken)
	{
		var sha256SumsFilePath = Path.Combine(_installerDir, "SHA256SUMS.asc");

		// This will throw InvalidOperationException in case of invalid signature.
		await DownloadAndValidateWasabiSignatureAsync(sha256SumsFilePath, asset.sha256sumsUrl, asset.wasabiSigUrl, cancellationToken).ConfigureAwait(false);

		var installerFilePath = Path.Combine(_installerDir, asset.FileName);

		try
		{
			if (!File.Exists(installerFilePath))
			{
				EnsureToRemoveCorruptedFiles();

				Logger.LogInfo($"Trying to download new version.");

				// Get file stream and copy it to downloads folder to access.
				using HttpRequestMessage request = new(HttpMethod.Get, asset.Asset.DownloadUri);
				using HttpResponseMessage response = await _githubHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
				byte[] installerFileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

				Logger.LogInfo("Installer downloaded, copying...");

				using MemoryStream stream = new(installerFileBytes);
				await CopyStreamContentToFileAsync(stream, installerFilePath, cancellationToken).ConfigureAwait(false);
			}
			string expectedHash = await GetHashFromSha256SumsFileAsync(asset.FileName, sha256SumsFilePath).ConfigureAwait(false);
			await VerifyInstallerHashAsync(installerFilePath, expectedHash, cancellationToken).ConfigureAwait(false);
		}
		catch (IOException)
		{
			cancellationToken.ThrowIfCancellationRequested();
			throw;
		}

		return installerFilePath;
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
		var response = await _githubHttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

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

	private async Task DownloadAndValidateWasabiSignatureAsync(string sha256SumsFilePath, Uri sha256SumsUrl, Uri wasabiSigUrl, CancellationToken cancellationToken)
	{
		var wasabiSigFilePath = Path.Combine(_installerDir, "SHA256SUMS.wasabisig");

		try
		{
			using HttpRequestMessage sha256Request = new(HttpMethod.Get, sha256SumsUrl);
			using HttpResponseMessage sha256Response = await _githubHttpClient.SendAsync(sha256Request, cancellationToken).ConfigureAwait(false);
			string sha256Content = await sha256Response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

			IoHelpers.EnsureContainingDirectoryExists(sha256SumsFilePath);
			File.WriteAllText(sha256SumsFilePath, sha256Content);

			using HttpRequestMessage signatureRequest = new(HttpMethod.Get, wasabiSigUrl);
			using HttpResponseMessage signatureResponse = await _githubHttpClient.SendAsync(signatureRequest, cancellationToken).ConfigureAwait(false);
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

	private (Asset asset, string fileName, Uri sha256sumsUrl, Uri wasabiSigUrl) GetAssetToDownload(List<Asset> assets)
	{
		Uri sha256SumsUrl = assets.First(asset => asset.Name.Equals("SHA256SUMS.asc")).DownloadUri;
		Uri wasabiSigUrl = assets.First(asset => asset.Name.Equals("SHA256SUMS.wasabisig")).DownloadUri;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			Asset msiAsset = assets.First(asset => asset.Name.Equals(".msi"));
			return (msiAsset, msiAsset.DownloadUri.ToString().Split("/").Last(), sha256SumsUrl, wasabiSigUrl);
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			var cpu = RuntimeInformation.ProcessArchitecture;
			if (cpu.ToString() == "Arm64")
			{
				Asset arm64Asset = assets.First(asset => asset.Name.Equals("arm64.dmg"));
				return (arm64Asset, arm64Asset.DownloadUri.ToString().Split("/").Last(), sha256SumsUrl, wasabiSigUrl);
			}
			Asset dmgAsset = assets.First(asset => asset.Name.Equals(".dmg"));
			return (dmgAsset, dmgAsset.DownloadUri.ToString().Split("/").Last(), sha256SumsUrl, wasabiSigUrl);
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
		DirectoryInfo folder = new(_installerDir);
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
			var folder = new DirectoryInfo(_installerDir);
			if (folder.Exists)
			{
				Directory.Delete(_installerDir, true);
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

using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Services;

public class UpdateManager
{
	private string InstallerName { get; set; } = "";
	private const byte MaxTries = 3;
	private const string ReleaseURL = "https://api.github.com/repos/zkSNACKs/WalletWasabi/releases/latest";

	public UpdateManager(string dataDir, IHttpClient httpClient)
	{
		DataDir = dataDir;
		DownloadsDir = Path.Combine(DataDir, "Downloads");
		HttpClient = httpClient;
	}

	public async void UpdateStatusChangedAsync(UpdateStatus updateStatus)
	{
		var tries = 0;
		bool updateAvailable = !updateStatus.ClientUpToDate || !updateStatus.BackendCompatible;
		Version targetVersion = updateStatus.ClientVersion;
		if (!updateAvailable)
		{
			return;
		}
		Logger.LogInfo($"Trying to download new version: {targetVersion}");
		do
		{
			tries++;
			try
			{
				bool isReadyToInstall = await GetInstallerAsync(targetVersion).ConfigureAwait(false);
				Logger.LogInfo($"Version {targetVersion} downloaded successfuly.");
				updateStatus.IsReadyToInstall = isReadyToInstall;
				break;
			}
			catch (Exception ex)
			{
				Logger.LogError($"Geting version {targetVersion} failed with error.", ex);
			}
		} while (tries < MaxTries);

		UpdateAvailableToGet?.Invoke(this, updateStatus);
	}

	private async Task<bool> GetInstallerAsync(Version targetVersion)
	{
		if (CheckIfInstallerDownloaded())
		{
			return true;
		}

		using HttpRequestMessage message = new(HttpMethod.Get, ReleaseURL);
		message.Headers.UserAgent.Add(new("WalletWasabi", "2.0"));
		var response = await HttpClient.SendAsync(message).ConfigureAwait(false);

		JObject jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
		string softwareVersion = jsonResponse["tag_name"]?.ToString();
		if (string.IsNullOrEmpty(softwareVersion))
		{
			throw new InvalidDataException("Endpoint gave back wrong json data or it's changed.");
		}

		// If the version we are looking for is not the one on github, somethings wrong.
		Version githubVersion = new(softwareVersion[1..]);
		Version shortGithubVersion = new(githubVersion.Major, githubVersion.Minor, githubVersion.Build);
		if (targetVersion != shortGithubVersion)
		{
			throw new InvalidDataException("Target version from backend does not match with the latest github release. This should be impossible.");
		}

		// Get all asset names and download urls to find the correct one.
		List<JToken> assetsInfos = jsonResponse["assets"].Children().ToList();
		List<string> assetDownloadUrls = new();
		foreach (JToken asset in assetsInfos)
		{
			assetDownloadUrls.Add(asset["browser_download_url"].ToString());
		}

		(string url, string fileName) = GetAssetToDownload(assetDownloadUrls);

		// This should also be done using Tor.
		using System.Net.Http.HttpClient httpClient = new();
		using HttpRequestMessage newMessage = new(HttpMethod.Get, url);
		message.Headers.UserAgent.Add(new("WalletWasabi", "2.0"));

		// Get file stream and copy it to downloads folder to access.
		var stream = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
		using var file = File.OpenWrite(Path.Combine(DownloadsDir, fileName));
		await stream.CopyToAsync(file).ConfigureAwait(false);
		InstallerName = fileName;

		return true;
	}

	private (string url, string fileName) GetAssetToDownload(List<string> urls)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var url = urls.Where(url => url.Contains(".msi")).First();
			return (url, url.Split("/").Last());
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			var cpu = RuntimeInformation.ProcessArchitecture;
			if (cpu.ToString() == "Arm64")
			{
				var arm64url = urls.Where(url => url.Contains("arm64.dmg")).First();
				return (arm64url, arm64url.Split("/").Last());
			}
			var url = urls.Where(url => url.Contains(".dmg") && !url.Contains("arm64")).First();
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

	public event EventHandler<UpdateStatus>? UpdateAvailableToGet;

	public string DataDir { get; }
	public string DownloadsDir { get; }
	public IHttpClient HttpClient { get; }
	public bool UpdateOnClose { get; set; }

	public bool InstallNewVersion()
	{
		try
		{
			var installerPath = Path.Combine(DataDir, "Downloads", InstallerName);
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

			using Process? p = Process.Start(startInfo);
			p!.WaitForExit();

			// Exit code -- Reason
			//	___________________
			//	1602	 -- Canceled
			//	1		 -- Terminated
			//	0		 -- Finished
			if (p.ExitCode == 0)
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					// For MacOS, you need to start the process twice, first start => permission denied
					// TODO: find out why and fix.
					p.Start();
					p!.WaitForExit();
				}
				else
				{
					Logger.LogInfo("Succesfuly installed new version. Deleting installer.");
					Directory.Delete(DownloadsDir, true);
					return true;
				}
			}
			else
			{
				Logger.LogError("Wasabi installer was terminated.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to install latest release. File might be corrupted. Deleting...", ex);
			Directory.Delete(DownloadsDir, true);
		}
		return false;
	}

	public bool CheckIfInstallerDownloaded()
	{
		var folder = new DirectoryInfo(DownloadsDir);
		if (folder.Exists)
		{
			var files = folder.GetFileSystemInfos();
			if (files.Length == 0)
			{
				return false;
			}
			return files.Any(file =>
			{
				if (file.Name.Contains("Wasabi"))
				{
					InstallerName = file.Name;
					return true;
				}
				return false;
			});
		}
		else
		{
			IoHelpers.EnsureDirectoryExists(DownloadsDir);
			return false;
		}
	}

	public void DeletePossibleLefotver()
	{
		var folder = new DirectoryInfo(DownloadsDir);
		if (folder.Exists)
		{
			Directory.Delete(DownloadsDir, true);
		}
	}
}

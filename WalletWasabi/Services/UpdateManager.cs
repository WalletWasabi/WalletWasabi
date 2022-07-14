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

	public UpdateManager(string dataDir, IHttpClient httpClient)
	{
		DataDir = dataDir;
		HttpClient = httpClient;
	}

	private async void UpdateChecker_UpdateStatusChangedAsync(object? sender, UpdateStatus updateStatus)
	{
		try
		{
			bool updateAvailable = !updateStatus.ClientUpToDate || !updateStatus.BackendCompatible;
			if (!updateAvailable)
			{
				return;
			}

			// TODO: download MSI to DataDir/downloads
			Version targetVersion = updateStatus.ClientVersion;
			bool downloadSuccessful = await GetInstallerAsync(targetVersion).ConfigureAwait(false);
			updateStatus.IsReadyToInstall = downloadSuccessful;
		}
		catch (Exception ex)
		{
			Logger.LogError("Geting new version failed with error.", ex);
		}

		UpdateAvailableToGet?.Invoke(this, updateStatus);
	}

	private async Task<bool> GetInstallerAsync(Version targetVersion)
	{
		using HttpRequestMessage message = new(HttpMethod.Get, "https://api.github.com/repos/zkSNACKs/WalletWasabi/releases/latest");
		message.Headers.UserAgent.Add(new("WalletWasabi", "2.0"));
		var resp = await HttpClient.SendAsync(message).ConfigureAwait(false);

		JObject jsonResponse = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));

		string softwareVersion = jsonResponse["tag_name"]?.ToString();
		if (string.IsNullOrEmpty(softwareVersion))
		{
			throw new InvalidDataException("");
		}

		Version githubVersion = new(softwareVersion[1..]);
		Version shortGithubVersion = new(githubVersion.Major, githubVersion.Minor, githubVersion.Build);
		if (targetVersion != shortGithubVersion)
		{
			throw new InvalidDataException("");
		}

		List<JToken> assetsInfos = jsonResponse["assets"].Children().ToList();
		List<string> assetDownloadUrls = new();
		foreach (JToken asset in assetsInfos)
		{
			assetDownloadUrls.Add(asset["browser_download_url"].ToString());
		}

		(string url, string fileName) = GetAssetToDownload(softwareVersion, assetDownloadUrls);

		var installerPath = Path.Combine(DataDir, "Downloads");
		IoHelpers.EnsureDirectoryExists(installerPath);

		using System.Net.Http.HttpClient httpClient = new();
		using HttpRequestMessage newMessage = new(HttpMethod.Get, url);
		message.Headers.UserAgent.Add(new("WalletWasabi", "2.0"));

		var stream = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
		using var file = File.OpenWrite(Path.Combine(installerPath, fileName));
		await stream.CopyToAsync(file).ConfigureAwait(false);
		InstallerName = fileName;

		return true;
	}

	private (string url, string fileName) GetAssetToDownload(string softwareVersion, List<string> urls)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var url = urls.Where(url => url.Contains(".msi")).First();
			return (url, url.Split("/").Last());
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			var url = urls.Where(url => url.Contains("arm64.dmg")).First();
			return (url, url.Split("/").Last());
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			if (RuntimeInformation.OSDescription.Contains("Ubuntu"))
			{
				var url = urls.Where(url => url.Contains(".deb")).First();
				return (url, url.Split("/").Last());
			}
			else
			{
				throw new InvalidOperationException("For Linux, get the correct update manually.");
			}
		}
		else
		{
			throw new InvalidOperationException("OS not recognized, download manually.");
		}
	}

	public event EventHandler<UpdateStatus>? UpdateAvailableToGet;

	public string DataDir { get; }
	public IHttpClient HttpClient { get; }
	public bool UpdateOnClose { get; set; }
	public UpdateChecker? UpdateChecker { get; set; }

	public void InstallNewVersion()
	{
		var installerPath = Path.Combine(DataDir, "Downloads", InstallerName);
		if (File.Exists(installerPath))
		{
			ProcessStartInfo startInfo = ProcessStartInfoFactory.Make(installerPath, "");
			// Throws exception rn
			using Process? p = Process.Start(startInfo);
		}
	}

	public void Initialize(UpdateChecker updateChecker)
	{
		UpdateChecker = updateChecker;
		updateChecker.UpdateStatusChanged += UpdateChecker_UpdateStatusChangedAsync;
	}

	public void Unsubscribe()
	{
		UpdateChecker!.UpdateStatusChanged -= UpdateChecker_UpdateStatusChangedAsync;
	}
}

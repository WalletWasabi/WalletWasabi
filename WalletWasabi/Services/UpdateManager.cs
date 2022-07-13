using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Microservices;
using WalletWasabi.Models;

namespace WalletWasabi.Services;

public class UpdateManager
{
	public UpdateManager(string dataDir)
	{
		DataDir = dataDir;
	}

	private void UpdateChecker_UpdateStatusChanged(object? sender, UpdateStatus updateStatus)
	{
		bool updateAvailable = !updateStatus.ClientUpToDate || !updateStatus.BackendCompatible;
		if (updateAvailable)
		{
			try
			{
				// TODO: download MSI to DataDir/downloads
				bool downloadSuccessful = GetInstaller(updateStatus);
				updateStatus.IsReadyToInstall = downloadSuccessful;
			}
			catch (Exception)
			{
				throw;
			}
			finally
			{
				UpdateAvailableToGet?.Invoke(this, updateStatus);
			}
		}
	}

	private bool GetInstaller(UpdateStatus updateStatus)
	{
		var resp = GetInstallerAsync(updateStatus);
		return resp.Result;
	}

	private async Task<bool> GetInstallerAsync(UpdateStatus updateStatus)
	{
		await Task.Delay(5000).ConfigureAwait(false);
		// Download installer here
		// based on target OS
		return true;
	}

	public event EventHandler<UpdateStatus>? UpdateAvailableToGet;

	public string DataDir { get; }
	public bool UpdateOnClose { get; set; }

	public void Initialize(UpdateChecker updateChecker)
	{
		updateChecker.UpdateStatusChanged += UpdateChecker_UpdateStatusChanged;
	}

	public void InstallNewVersion()
	{
		var installerPath = Path.Combine(DataDir, "Downloads", "<downloaded_installer_name>");
		if (File.Exists(installerPath))
		{
			ProcessStartInfo startInfo = ProcessStartInfoFactory.Make(installerPath, "");
			using Process? p = Process.Start(startInfo);
		}
	}
}

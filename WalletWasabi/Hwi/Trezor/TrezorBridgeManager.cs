using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// Owns the lifecycle of a standalone Trezor Bridge (trezord) so coinjoin can reach the device without the
/// user starting anything, and so it gets out of the way when HWI needs exclusive USB access.
///
/// Coinjoin needs the bridge (HWI cannot unlock SLIP-25); HWI (used to detect a device while adding a wallet)
/// needs direct USB access — and only one can hold the Trezor at a time. This manager starts trezord when a
/// coinjoin capable Trezor wallet is loaded and stops it when that wallet is closed or when the app is about to
/// enumerate devices with HWI. It never touches a bridge it did not start (e.g. one provided by Trezor Suite):
/// if a bridge is already reachable it is reused as is.
/// </summary>
public enum TrezorBridgeStatus
{
	/// <summary>No bridge involved: the device is reached over direct USB (HWI).</summary>
	NotRunning,

	/// <summary>A trezord started and owned by Wasabi is serving the device.</summary>
	StartedByWasabi,

	/// <summary>A bridge Wasabi did not start (e.g. Trezor Suite) is serving the device.</summary>
	External,
}

public static class TrezorBridgeManager
{
	/// <summary>
	/// Official Trezor Suite releases, offered when no bridge is running. Standalone trezord-go is
	/// deprecated and publishes no releases anymore; the bridge now ships inside Trezor Suite. An already
	/// installed standalone trezord keeps working and is still auto-started when found.
	/// </summary>
	public const string SuiteDownloadUrl = "https://github.com/trezor/trezor-suite/releases/latest";

	private static readonly SemaphoreSlim Lock = new(1, 1);
	private static Process? _ourProcess;
	private static TrezorBridgeStatus _status;

	/// <summary>Raised when the way the Trezor is reached changes, so the UI can show it.</summary>
	public static event EventHandler<TrezorBridgeStatus>? StatusChanged;

	public static TrezorBridgeStatus Status
	{
		get => _status;
		private set
		{
			if (_status != value)
			{
				_status = value;
				StatusChanged?.Invoke(null, value);
			}
		}
	}

	/// <summary>Ensures a bridge is reachable, starting our own trezord only if none is already running.</summary>
	public static async Task EnsureRunningAsync(CancellationToken cancellationToken)
	{
		await Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (IsOurProcessAlive())
			{
				Status = TrezorBridgeStatus.StartedByWasabi;
				return;
			}

			if (await TrezorDevice.IsBridgeAvailableAsync(cancellationToken).ConfigureAwait(false))
			{
				// A bridge is already running (Trezor Suite or a user-started trezord); leave it alone.
				Status = TrezorBridgeStatus.External;
				return;
			}

			if (FindTrezordExecutable() is not { } executable)
			{
				Logger.LogInfo($"Trezor Bridge (trezord) was not found. Coinjoin needs Trezor Suite running, which includes the bridge; download from {SuiteDownloadUrl}.");
				Status = TrezorBridgeStatus.NotRunning;
				return;
			}

			_ourProcess = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true });
			Logger.LogInfo("Started Trezor Bridge for coinjoin.");
			Status = TrezorBridgeStatus.StartedByWasabi;

			// Give trezord a moment to bind its port before the first request.
			await WaitForBridgeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Could not start Trezor Bridge: {ex.Message}");
		}
		finally
		{
			Lock.Release();
		}
	}

	/// <summary>Stops the trezord we started (if any), freeing the USB device for HWI. Bridges we did not start are left running.</summary>
	public static void StopIfOurs()
	{
		Lock.Wait();
		try
		{
			if (!IsOurProcessAlive())
			{
				_ourProcess = null;
				if (Status == TrezorBridgeStatus.StartedByWasabi)
				{
					Status = TrezorBridgeStatus.NotRunning;
				}
				return;
			}

			try
			{
				_ourProcess!.Kill(entireProcessTree: true);
				_ourProcess.WaitForExit(3000);
				Logger.LogInfo("Stopped the Trezor Bridge we started.");
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"Could not stop Trezor Bridge: {ex.Message}");
			}
			finally
			{
				_ourProcess?.Dispose();
				_ourProcess = null;
				Status = TrezorBridgeStatus.NotRunning;
			}
		}
		finally
		{
			Lock.Release();
		}
	}

	private static bool IsOurProcessAlive()
	{
		try
		{
			return _ourProcess is { HasExited: false };
		}
		catch
		{
			return false;
		}
	}

	private static async Task WaitForBridgeAsync(CancellationToken cancellationToken)
	{
		for (int i = 0; i < 20; i++)
		{
			if (await TrezorDevice.IsBridgeAvailableAsync(cancellationToken).ConfigureAwait(false))
			{
				return;
			}
			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}
	}

	private static string? FindTrezordExecutable()
	{
		IEnumerable<string> candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			?
			[
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TREZOR Bridge", "trezord.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TREZOR Bridge", "trezord.exe"),
			]
			: RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
				? ["/Applications/Utilities/TREZOR Bridge/trezord", "/usr/local/bin/trezord"]
				: ["/usr/bin/trezord", "/usr/local/bin/trezord"];

		foreach (var candidate in candidates)
		{
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}
		return null;
	}
}

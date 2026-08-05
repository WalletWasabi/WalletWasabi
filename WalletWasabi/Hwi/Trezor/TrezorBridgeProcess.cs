using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>How the Trezor is currently reachable.</summary>
public enum HardwareWalletTransport
{
	/// <summary>No bridge involved: the device is reached over direct USB.</summary>
	DirectUsb,

	/// <summary>A bridge started and owned by Wasabi is serving the device.</summary>
	BridgeStartedByWasabi,

	/// <summary>A bridge Wasabi did not start (e.g. one from the vendor's own software) is serving the device.</summary>
	ExternalBridge,
}

/// <summary>
/// Owns the lifecycle of a standalone Trezor Bridge (trezord) process.
///
/// Coinjoin needs the bridge (HWI cannot unlock SLIP-25); HWI needs direct USB access - and only one of them
/// can hold the device at a time. This starts trezord when the bridge is needed and stops it when the device
/// has to be handed back. A bridge it did not start (e.g. one provided by Trezor Suite) is never touched:
/// if a bridge is already reachable it is reused as is.
///
/// Callers do not use this directly: the hardware wallet service owns an instance and takes care of the
/// handover, so that no caller has to remember a bridge exists.
/// </summary>
public class TrezorBridgeProcess : IDisposable
{
	/// <summary>
	/// Official Trezor Suite releases, offered when no bridge is running. Standalone trezord-go is
	/// deprecated and publishes no releases anymore; the bridge now ships inside Trezor Suite. An already
	/// installed standalone trezord keeps working and is still auto-started when found.
	/// </summary>
	public const string SuiteDownloadUrl = "https://github.com/trezor/trezor-suite/releases/latest";

	private readonly SemaphoreSlim _lock = new(1, 1);
	private Process? _ourProcess;
	private HardwareWalletTransport _status;

	/// <summary>Raised when the way the Trezor is reached changes, so the UI can show it.</summary>
	public event EventHandler<HardwareWalletTransport>? StatusChanged;

	public HardwareWalletTransport Status
	{
		get => _status;
		private set
		{
			if (_status != value)
			{
				_status = value;
				StatusChanged?.Invoke(this, value);
			}
		}
	}

	/// <summary>Ensures a bridge is reachable, starting our own trezord only if none is already running.</summary>
	public async Task EnsureRunningAsync(CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (IsOurProcessAlive())
			{
				Status = HardwareWalletTransport.BridgeStartedByWasabi;
				return;
			}

			if (await TrezorDevice.IsBridgeAvailableAsync(cancellationToken).ConfigureAwait(false))
			{
				// A bridge is already running (Trezor Suite or a user-started trezord); leave it alone.
				Status = HardwareWalletTransport.ExternalBridge;
				return;
			}

			if (FindTrezordExecutable() is not { } executable)
			{
				Logger.LogInfo($"Trezor Bridge (trezord) was not found. Coinjoin needs Trezor Suite running, which includes the bridge; download from {SuiteDownloadUrl}.");
				Status = HardwareWalletTransport.DirectUsb;
				return;
			}

			_ourProcess = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true });
			Logger.LogInfo("Started Trezor Bridge for coinjoin.");
			Status = HardwareWalletTransport.BridgeStartedByWasabi;

			// Give trezord a moment to bind its port before the first request.
			await WaitForBridgeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"Could not start Trezor Bridge: {ex.Message}");
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// Stops the trezord we started (if any), freeing the USB device for HWI. Bridges we did not start are
	/// left running. Returns whether a bridge of ours was actually stopped, so the caller can put it back.
	/// </summary>
	public bool StopIfOurs()
	{
		_lock.Wait();
		try
		{
			if (!IsOurProcessAlive())
			{
				_ourProcess = null;
				if (Status == HardwareWalletTransport.BridgeStartedByWasabi)
				{
					Status = HardwareWalletTransport.DirectUsb;
				}
				return false;
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
				Status = HardwareWalletTransport.DirectUsb;
			}

			return true;
		}
		finally
		{
			_lock.Release();
		}
	}

	private bool IsOurProcessAlive()
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

	public void Dispose()
	{
		StopIfOurs();
		_lock.Dispose();
	}
}

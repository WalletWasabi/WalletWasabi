using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace WalletWasabi.Hwi.Trezor;

/// <summary>
/// Starts a standalone trezord when no bridge is running (coinjoin needs it, HWI cannot unlock SLIP-25) and stops
/// it again when HWI has to take the USB device. A bridge it did not start, e.g. Trezor Suite's, is left alone.
/// The hardware wallet service owns the instance and does the handover, so no caller has to know a bridge exists.
/// </summary>
public class TrezorBridgeProcess : IDisposable
{
	/// <summary>Offered when no bridge is running: standalone trezord-go is deprecated, the bridge now ships inside Trezor Suite.</summary>
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

	/// <summary>Stops the trezord we started, freeing the USB device for HWI; returns whether one was stopped, so the caller can put it back.</summary>
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

		return candidates.FirstOrDefault(File.Exists);
	}

	public void Dispose()
	{
		StopIfOurs();
		_lock.Dispose();
	}
}

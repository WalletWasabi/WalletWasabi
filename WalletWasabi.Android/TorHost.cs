using Android.Content;
using Android.OS;
using System.Net;
using System.Net.Sockets;
using Java.Lang;
using WalletWasabi.Models;
using WalletWasabi.Mobile;

namespace WalletWasabi.Android;

internal sealed class TorHost : IAsyncDisposable
{
	private Java.Lang.Process? _process;
	private Task? _output;
	private readonly CancellationTokenSource _stop = new();
	private bool _regtest;
	private int _disposed;
	private int _bootstrap;
	private readonly object _diagnosticGate = new();
	private readonly Queue<string> _diagnostics = new();
	public int Bootstrap { get => Volatile.Read(ref _bootstrap); private set => Volatile.Write(ref _bootstrap, value); }
	// A bounded, in-memory notice trace. Only synthetic instrumentation exports it.
	internal string Diagnostics { get { lock (_diagnosticGate) { return string.Join('\n', _diagnostics); } } }
	public bool IsAlive
	{
		get
		{
			if (Volatile.Read(ref _disposed) != 0) { return false; }
			if (_regtest) { return !_stop.IsCancellationRequested; }
			if (_process is not { } process) { return false; }
			if (OperatingSystem.IsAndroidVersionAtLeast(26)) { return process.IsAlive; }
			try { _ = process.ExitValue(); return false; } catch (IllegalThreadStateException) { return true; }
		}
	}

	public async Task StartAsync(Context context, string dataDir, MobileSettings settings, CancellationToken token)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
		if (_process is not null || _regtest) { throw new InvalidOperationException("Tor has already started."); }
		if (settings.GetNetwork() == NBitcoin.Network.RegTest)
		{
			_regtest = true;
			Bootstrap = 100;
			return;
		}
		Directory.CreateDirectory(Path.Combine(dataDir, "tor"));
		foreach (var name in new[] { "geoip", "geoip6" })
		{
			var target = Path.Combine(dataDir, "tor", name);
			// Windows checkouts contain CRLF. The bundled Android Tor parser
			// rejects those country records, flooding stdout during bootstrap.
			// Re-copy atomically so previous installations receive the correction.
			var temporary = target + ".new";
			try
			{
				using var source = context.Assets!.Open("Tor/" + name);
				using var reader = new StreamReader(source);
				await using (var destination = new StreamWriter(temporary, false, new System.Text.UTF8Encoding(false)) { NewLine = "\n" })
				{
					while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
					{ await destination.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false); }
				}
				File.Move(temporary, target, true);
			}
			finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
		}
		var executable = Path.Combine(context.ApplicationInfo!.NativeLibraryDir!, "libtor.so");
		if (!File.Exists(executable))
		{
			throw new InvalidOperationException("The Android Tor executable is missing.");
		}
		var args = new[]
		{
			executable, "--ignore-missing-torrc", "-f", Path.Combine(dataDir, "tor", "torrc"),
			"--DataDirectory", Path.Combine(dataDir, "tor", "state"),
			"--SOCKSPort", $"127.0.0.1:{AppIdentity.SocksPort} ExtendedErrors KeepAliveIsolateSOCKSAuth",
			"--ControlPort", $"127.0.0.1:{AppIdentity.ControlPort}", "--CookieAuthentication", "1",
			"--CookieAuthFile", Path.Combine(dataDir, $"control_auth_cookie_{AppIdentity.SocksPort}_{AppIdentity.ControlPort}"),
			"--GeoIPFile", Path.Combine(dataDir, "tor", "geoip"),
			"--GeoIPv6File", Path.Combine(dataDir, "tor", "geoip6"),
			"--ClientOnly", "1", "--SafeLogging", "1", "--Log", "notice stdout",
			"--__OwningControllerProcess", System.Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)
		};
		using var processBuilder = new ProcessBuilder(args);
		processBuilder.RedirectErrorStream(true);
		var process = processBuilder.Start() ?? throw new InvalidOperationException("Tor could not start.");
		_process = process;
		_output = Task.Run(async () =>
		{
			using var reader = new StreamReader(process.InputStream!);
			while (!_stop.IsCancellationRequested && await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
			{
				lock (_diagnosticGate)
				{
					if (_diagnostics.Count == 60) { _diagnostics.Dequeue(); }
					_diagnostics.Enqueue(line.Length > 2048 ? line[..2048] : line);
				}
				var marker = line.IndexOf("Bootstrapped ", StringComparison.Ordinal);
				if (marker >= 0 && int.TryParse(line.AsSpan(marker + 13).ToString().Split('%')[0], out var progress))
				{
					Bootstrap = progress;
				}
			}
		}, _stop.Token);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
		deadline.CancelAfter(TimeSpan.FromMinutes(3));
		while (Bootstrap < 100)
		{
			deadline.Token.ThrowIfCancellationRequested();
			if (!IsAlive)
			{
				throw new InvalidOperationException("Tor could not start. Reopen Wasabi to retry.");
			}
			await Task.Delay(250, deadline.Token).ConfigureAwait(false);
		}
		using var tcp = new TcpClient();
		await tcp.ConnectAsync(IPAddress.Loopback, AppIdentity.SocksPort, deadline.Token).ConfigureAwait(false);
		await tcp.GetStream().WriteAsync(new byte[] { 5, 1, 0 }, deadline.Token).ConfigureAwait(false);
		var response = new byte[2];
		await tcp.GetStream().ReadExactlyAsync(response, deadline.Token).ConfigureAwait(false);
		if (response is not [5, 0])
		{
			throw new InvalidOperationException("Tor SOCKS verification failed.");
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
		await _stop.CancelAsync().ConfigureAwait(false);
		var process = Interlocked.Exchange(ref _process, null);
		Bootstrap = 0;
		process?.Destroy();
		if (_output is { } output)
		{
			try { await output.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
			catch (System.Exception) { }
		}
		process?.Dispose();
		_stop.Dispose();
	}
}

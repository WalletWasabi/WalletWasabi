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
	public int Bootstrap { get; private set; }

	public async Task StartAsync(Context context, string dataDir, MobileSettings settings, CancellationToken token)
	{
		if (settings.GetNetwork() == NBitcoin.Network.RegTest)
		{
			Bootstrap = 100;
			return;
		}
		Directory.CreateDirectory(Path.Combine(dataDir, "tor"));
		foreach (var name in new[] { "geoip", "geoip6" })
		{
			var target = Path.Combine(dataDir, "tor", name);
			if (!File.Exists(target))
			{
				using var source = context.Assets!.Open("Tor/" + name);
				await using var destination = File.Create(target);
				await source.CopyToAsync(destination, token).ConfigureAwait(false);
			}
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
			"--SOCKSPort", "127.0.0.1:37154 ExtendedErrors KeepAliveIsolateSOCKSAuth",
			"--ControlPort", "127.0.0.1:37155", "--CookieAuthentication", "1",
			"--CookieAuthFile", Path.Combine(dataDir, "control_auth_cookie_37154_37155"),
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
			if (OperatingSystem.IsAndroidVersionAtLeast(26) && !process.IsAlive)
			{
				throw new InvalidOperationException("Tor could not start. Reopen Wasabi to retry.");
			}
			await Task.Delay(250, deadline.Token).ConfigureAwait(false);
		}
		using var tcp = new TcpClient();
		await tcp.ConnectAsync(IPAddress.Loopback, 37154, deadline.Token).ConfigureAwait(false);
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
		await _stop.CancelAsync().ConfigureAwait(false);
		_process?.Destroy();
		if (_output is { } output)
		{
			try { await output.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
			catch (System.Exception) { }
		}
		_process?.Dispose();
		_stop.Dispose();
	}
}

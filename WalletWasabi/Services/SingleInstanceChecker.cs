using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services;

public enum WasabiInstanceStatus
{
	Error,
	AnotherInstanceIsRunning,
	NoOtherInstanceIsRunning,
}

public class SingleInstanceChecker : BackgroundService, IAsyncDisposable
{
	private const string WasabiMagicString = "InBitcoinWeTrust";
	public static readonly TimeSpan ClientTimeOut = TimeSpan.FromSeconds(2);

	/// <summary>Multiplier to be applied to all timeouts in this class.</summary>
	private readonly int _timeoutMultiplier;

	/// <summary>
	/// Creates an object to ensure mutual exclusion of Wasabi instances per Network <paramref name="network"/>.
	/// The solution based on TCP socket.
	/// </summary>
	/// <param name="network">Bitcoin network selected when Wasabi Wallet was started. It will use the port 37129, 37130, 37131 according to network main, test, reg.</param>
	public SingleInstanceChecker(Network network) : this(NetworkToPort(network))
	{
	}

	/// <summary>
	/// Use this constructor only for testing.
	/// </summary>
	/// <param name="timeoutMultiplier">Used for multiplying all specified timeouts. For CI testing purposes only.</param>
	public SingleInstanceChecker(int port, int timeoutMultiplier = 1)
	{
		_port = port;
		_timeoutMultiplier = timeoutMultiplier;
	}

	public event EventHandler? OtherInstanceStarted;

	private readonly int _port;

	private readonly CancellationTokenSource _disposeCts = new();
	private TaskCompletionSource? TaskStartTcpListener { get; set; }

	public async Task<WasabiInstanceStatus> CheckSingleInstanceAsync()
	{
		// Start single instance checker that is active over the lifetime of the application.
		try
		{
			var singleInstanceResult = await CanRunAsSingleInstanceAsync().ConfigureAwait(false);
			return singleInstanceResult
				? WasabiInstanceStatus.NoOtherInstanceIsRunning
				: WasabiInstanceStatus.AnotherInstanceIsRunning;
		}
		catch (Exception e)
		{
			Logger.LogError(e);
			return WasabiInstanceStatus.Error;
		}
	}

	/// <summary>
	/// This function verifies whether is the only instance running on this machine or not. In case of secondary start
	/// we try to signal the first instance before returning false.
	/// On macOS this function will never fail if you run Wasabi as a macApp, because mac prevents running the same APP multiple times on OS level.
	/// </summary>
	/// <returns>true if this is the only instance running; otherwise false.</returns>
	private async Task<bool> CanRunAsSingleInstanceAsync()
	{
        ObjectDisposedException.ThrowIf(_disposeCts.IsCancellationRequested, this);

		try
		{
			TaskStartTcpListener = new TaskCompletionSource();

			// Start ExecuteAsync.
			await StartAsync(_disposeCts.Token).ConfigureAwait(false);

			// Wait for the result of TcpListener.Start().
			await TaskStartTcpListener.Task.WaitAsync(_disposeCts.Token).ConfigureAwait(false);

			// This is the first instance, nothing else to do.
			return true;
		}
		catch (SocketException ex) when (ex.ErrorCode is 10048 or 48 or 98)
		{
			// ErrorCodes are different on every OS: win, macOS, Linux.
			// It is already used -> another Wasabi is running on this network.
			Logger.LogDebug("Another Wasabi instance is already running.");
		}

		// Signal to the other instance, that there was an attempt to start the software.
		using TcpClient client = new()
		{
			NoDelay = true
		};

		using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(_timeoutMultiplier * 10));
		using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, timeoutCts.Token);

		await client.ConnectAsync(IPAddress.Loopback, _port, cts.Token).ConfigureAwait(false);

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
		await using NetworkStream networkStream = client.GetStream();
		networkStream.WriteTimeout = _timeoutMultiplier * (int)ClientTimeOut.TotalMilliseconds * 2;
		await using var writer = new StreamWriter(networkStream, Encoding.UTF8);
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

		await writer.WriteAsync(WasabiMagicString.AsMemory(), cts.Token).ConfigureAwait(false);
		await writer.FlushAsync().ConfigureAwait(false);
		await networkStream.FlushAsync(cts.Token).ConfigureAwait(false);

		// I was able to signal to the other instance successfully so just continue.
		return false;
	}

	private static int NetworkToPort(Network network) => network switch
	{
		_ when network == Network.Main => 37129,
		_ when network == Network.TestNet => 37130,
		_ when network == Network.RegTest => 37131,
		_ when network == Bitcoin.Instance.Signet => 37132,
		_ => throw new Exception($"Network {network} is unknown")
	};

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var task = TaskStartTcpListener
			?? throw new InvalidOperationException("This should never happen!");

		try
		{
            using TcpListener listener = new(IPAddress.Loopback, _port)
			{
				ExclusiveAddressUse = true
			};

			// This can throw an exception if the port is already open.
			listener.Start(0);

			// Indicate that the Listener is created successfully.
			task.TrySetResult();

			while (!stoppingToken.IsCancellationRequested)
			{
				// In case of cancellation, listener.Stop will cause AcceptTcpClientAsync to throw, thus cancelling it.
				using var client = await listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
				client.ReceiveBufferSize = 1000;
				try
				{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
					await using NetworkStream networkStream = client.GetStream();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

					networkStream.ReadTimeout = _timeoutMultiplier * (int)ClientTimeOut.TotalMilliseconds;
					using var reader = new StreamReader(networkStream, Encoding.UTF8);

					// Make sure the client will be disconnected.
					using CancellationTokenSource timeOutCts = new(_timeoutMultiplier * ClientTimeOut);
					using var cts = CancellationTokenSource.CreateLinkedTokenSource(timeOutCts.Token, stoppingToken);

					// The read operation cancellation will happen on reader disposal.
					string answer = await reader.ReadToEndAsync(cts.Token).ConfigureAwait(false);
					if (answer == WasabiMagicString)
					{
						Logger.LogInfo($"Detected another Wasabi instance.");
						OtherInstanceStarted?.Invoke(this, EventArgs.Empty);
					}
				}
				catch (Exception ex)
				{
					// Somebody connected but it was not another Wasabi instance.
					Logger.LogDebug(ex);
				}
			}
		}
		catch (Exception ex)
		{
			// Indicate that there was an error.
			task.TrySetException(ex);
			return;
		}
	}

	public async ValueTask DisposeAsync()
	{
		_disposeCts.Cancel();

		await StopAsync(CancellationToken.None).ConfigureAwait(false);

		_disposeCts.Dispose();
	}

	public override void Dispose()
	{
		base.Dispose();
		_disposeCts.Cancel();

		// Stopping the execution task and wait until it finishes.
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(_timeoutMultiplier * 20));

        // This is added because Dispose is called from the Main and Main cannot be an async function.
        if (ExecuteTask is not null)
        {
            while (!ExecuteTask.IsCompleted)
            {
                Thread.Sleep(10);
                if (timeout.IsCancellationRequested)
                {
                    Logger.LogWarning($"{nameof(SingleInstanceChecker)} cannot be disposed properly in time.");
                    break;
                }
            }
        }

		_disposeCts.Dispose();
	}
}

using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class SingleInstanceChecker : BackgroundService, IAsyncDisposable
	{
		private const string WasabiMagicString = "InCryptoWeTrust";
		public static readonly TimeSpan ClientTimeOut = TimeSpan.FromSeconds(2);

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
		public SingleInstanceChecker(int port)
		{
			Port = port;
		}

		private int Port { get; }

		private CancellationTokenSource DisposeCts { get; } = new();
		private TaskCompletionSource? TaskStartTcpListener { get; set; }

		public event EventHandler? OtherInstanceStarted;

		/// <summary>
		/// This function ensures that this is the only instance running on this machine or throws an exception if it is not. In case of secondary start
		/// we try to signal the first instance before throwing the exception.
		/// On macOS this function will never throw if you run Wasabi as a macApp, because mac prevents running the same APP multiple times on OS level.
		/// </summary>
		/// <exception cref="InvalidOperationException">Wasabi is already running, signaling the first instance failed.</exception>
		/// <exception cref="OperationCanceledException">Wasabi is already running and signaled.</exception>
		public async Task EnsureSingleOrThrowAsync()
		{
			if (DisposeCts.IsCancellationRequested)
			{
				throw new ObjectDisposedException(nameof(SingleInstanceChecker));
			}

			try
			{
				TaskStartTcpListener = new TaskCompletionSource();

				// Start ExecuteAsync.
				await StartAsync(DisposeCts.Token).ConfigureAwait(false);

				// Wait for the result of TcpListener.Start().
				await TaskStartTcpListener.Task.WithAwaitCancellationAsync(DisposeCts.Token).ConfigureAwait(false);

				// This is the first instance, nothing else to do.
				return;
			}
			catch (SocketException ex) when (ex.ErrorCode is 10048 or 48 or 98)
			{
				// ErrorCodes are different on every OS: win, macOS, Linux.
				// It is already used -> another Wasabi is running on this network.
				Logger.LogDebug("Another Wasabi instance is already running.");
			}

			try
			{
				// Signal to the other instance, that there was an attempt to start the software.
				using TcpClient client = new()
				{
					NoDelay = true
				};

				using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
				using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(DisposeCts.Token, timeoutCts.Token);

				await client.ConnectAsync(IPAddress.Loopback, Port, cts.Token).ConfigureAwait(false);

				await using NetworkStream networkStream = client.GetStream();

				networkStream.WriteTimeout = (int)ClientTimeOut.TotalMilliseconds * 2;
				await using var writer = new StreamWriter(networkStream, Encoding.UTF8);
				await writer.WriteAsync(new StringBuilder(WasabiMagicString), cts.Token).ConfigureAwait(false);
				await writer.FlushAsync().ConfigureAwait(false);
				await networkStream.FlushAsync(cts.Token).ConfigureAwait(false);
				// I was able to signal to the other instance successfully so just continue.
			}
			catch (Exception ex)
			{
				// Do not log anything here as the first instance is writing the Log at this time.
				throw new InvalidOperationException($"Wasabi is already running, but cannot be signaled, reason: '{ex}'");
			}

			throw new OperationCanceledException($"Wasabi is already running, signaled the first instance.");
		}

		private static int NetworkToPort(Network network) => network switch
		{
			_ when network == Network.Main => 37129,
			_ when network == Network.TestNet => 37130,
			_ when network == Network.RegTest => 37131,
			_ => throw new Exception($"Network {network} is unknown")
		};

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var task = TaskStartTcpListener;
			if (task is null)
			{
				throw new InvalidOperationException("This should never happen!");
			}

			TcpListener? listener = null;
			try
			{
				listener = new(IPAddress.Loopback, Port)
				{
					ExclusiveAddressUse = true
				};

				// This can throw an exception if the port is already open.
				listener.Start(0);

				// Indicate that the Listener is created successfully.
				task.TrySetResult();

				while (!stoppingToken.IsCancellationRequested)
				{
					// In case of cancellation, listener.Stop will cause AcceptTcpClientAsync to throw, thus canceling it.
					using var client = await listener.AcceptTcpClientAsync().WithAwaitCancellationAsync(stoppingToken).ConfigureAwait(false);
					client.ReceiveBufferSize = 1000;
					try
					{
						await using NetworkStream networkStream = client.GetStream();
						networkStream.ReadTimeout = (int)ClientTimeOut.TotalMilliseconds;
						using var reader = new StreamReader(networkStream, Encoding.UTF8);
						// Make sure the client will be disconnected.
						using CancellationTokenSource timeOutCts = new(ClientTimeOut);
						using var cts = CancellationTokenSource.CreateLinkedTokenSource(timeOutCts.Token, stoppingToken);

						// The read operation cancellation will happen on reader disposal.
						string answer = await reader.ReadToEndAsync().WithAwaitCancellationAsync(cts.Token).ConfigureAwait(false);
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
			finally
			{
				listener?.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			DisposeCts.Cancel();

			await StopAsync(CancellationToken.None).ConfigureAwait(false);

			DisposeCts.Dispose();
		}
	}
}

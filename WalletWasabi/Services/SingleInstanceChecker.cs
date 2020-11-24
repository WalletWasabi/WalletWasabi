using Microsoft.Extensions.Hosting;
using NBitcoin;
using Nito.AsyncEx;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class SingleInstanceChecker : BackgroundService, IAsyncDisposable
	{
		/// <summary>
		/// Creates an object to ensure mutual exclusion of Wasabi instances per Network <paramref name="network"/>.
		/// The solution based on TCP socket.
		/// </summary>
		/// <param name="network">Bitcoin network selected when Wasabi Wallet was started. It will use the port 37129,37130,37131 according to network main,test,reg.</param>
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
				// ErrorCodes are different on every OS: win, macOS, linux.
				// It is already used -> another Wasabi is running on this network.
				Logger.LogDebug("Detected another Wasabi instance.");
			}

			try
			{
				// Signal to the other instance, that there was an attempt to start the software.
				using TcpClient client = new TcpClient();
				await client.ConnectAsync(IPAddress.Loopback, Port).ConfigureAwait(false);
				// I was able to signal to the other instance successfully so just continue.
			}
			catch (Exception)
			{
				// Do not log anything here as the first instance is writing the Log at this time.
				throw new InvalidOperationException($"Wasabi is already running.");
			}

			throw new OperationCanceledException($"Wasabi is already running, signalled the first instance.");
		}

		private static int NetworkToPort(Network network)
		{
			if (network == Network.Main)
			{
				return 37129;
			}
			else if (network == Network.TestNet)
			{
				return 37130;
			}

			return 37131;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var task = TaskStartTcpListener;
			if (task is null)
			{
				Logger.LogError("This is impossible!");
				return;
			}

			var listener = new TcpListener(IPAddress.Loopback, Port);
			try
			{
				// This can throw an exception if the port is already open.
				listener.Start();

				// Indicate that the Listener is created successfully.
				task.TrySetResult();

				// Stop listener here to ensure thread-safety.
				using var _ = stoppingToken.Register(() => listener.Stop());

				while (!stoppingToken.IsCancellationRequested)
				{
					await listener.AcceptTcpClientAsync().ConfigureAwait(false);
					Logger.LogInfo($"Detected another Wasabi instance.");
					OtherInstanceStarted?.Invoke(this, EventArgs.Empty);
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
				listener.Stop();
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

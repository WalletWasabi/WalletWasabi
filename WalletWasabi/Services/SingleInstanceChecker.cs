using Microsoft.Extensions.Hosting;
using NBitcoin;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class SingleInstanceChecker : BackgroundService
	{
		private const string PipeNamePrefix = "WalletWasabiSingleInstance";

		/// <summary>
		/// Creates a new instance of the object where lock name is based on <paramref name="network"/> name.
		/// </summary>
		/// <param name="network">Bitcoin network selected when Wasabi Wallet was started.</param>
		public SingleInstanceChecker(Network network) : this(network, network.ToString())
		{
		}

		/// <summary>
		/// Use this constructor only for testing.
		/// </summary>
		/// <param name="network">Bitcoin network selected when Wasabi Wallet was started.</param>
		/// <param name="lockName">Postfix for the lock name</param>
		public SingleInstanceChecker(Network network, string lockName)
		{
			Network = network;
			PipeName = $"{PipeNamePrefix}-{lockName}";
		}

		private Network Network { get; }

		private string PipeName { get; }

		private CancellationTokenSource DisposeCts { get; } = new CancellationTokenSource();

		private bool FirstInstance => NamedPipeServerStream is { };

		private NamedPipeServerStream? NamedPipeServerStream { get; set; }

		public async Task CheckAsync()
		{
			if (DisposeCts.IsCancellationRequested)
			{
				throw new ObjectDisposedException(nameof(SingleInstanceChecker));
			}

			try
			{
				NamedPipeServerStream = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

				// Start listening for ClientPipes with ExecuteAsync.
				await StartAsync(DisposeCts.Token).ConfigureAwait(false);

				// This is the first instance of Wasabi we can return.
				return;
			}
			catch (IOException ex)
			{
				// There is another instance already running.
				Logger.LogDebug($"Could not create {nameof(NamedPipeServerStream)} reason '{ex}'.");
			}

			// Try to signal the other instance by connecting to it.
			// "." to specify the local computer.
			using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
			try
			{
				// Just make a connection and close the client.
				await client.ConnectAsync(2000, DisposeCts.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}

			throw new InvalidOperationException($"Wasabi is already running on {Network}!");
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				using var server = NamedPipeServerStream;
				if (server is null)
				{
					throw new InvalidOperationException();
				}

				while (!stoppingToken.IsCancellationRequested)
				{
					await server.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
					Logger.LogInfo("Got a connection!");

					// Disconnect the client to be able to wait for another connection.
					server.Disconnect();
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				// Something happened we are not trying to recover the NamedPipeServerStream.
				Logger.LogError(ex);
			}
		}

		public override void Dispose()
		{
			DisposeCts.Dispose();
			base.Dispose();
		}
	}
}

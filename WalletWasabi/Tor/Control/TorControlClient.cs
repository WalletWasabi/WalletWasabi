using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control
{
	/// <summary>
	/// Client already authenticated to Tor Control.
	/// </summary>
	public class TorControlClient : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public TorControlClient(TcpClient tcpClient)
		{
			TcpClient = tcpClient;

			PipeReader = PipeReader.Create(tcpClient.GetStream());
			PipeWriter = PipeWriter.Create(tcpClient.GetStream());
		}

		/// <summary>For testing.</summary>
		internal TorControlClient(PipeReader pipeReader, PipeWriter pipeWriter)
		{
			TcpClient = null;
			PipeReader = pipeReader;
			PipeWriter = pipeWriter;
		}

		private TcpClient? TcpClient { get; }
		private PipeReader PipeReader { get; }
		private PipeWriter PipeWriter { get; }

		/// <summary>
		/// Gets protocol info (for version 1).
		/// </summary>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">3.21. PROTOCOLINFO</seealso>
		public async Task<ProtocolInfoReply> GetProtocolInfoAsync(CancellationToken cancellationToken = default)
		{
			// Grammar: "PROTOCOLINFO" *(SP PIVERSION) CRLF
			// Note: PIVERSION is there in case we drastically change the syntax one day. For now it should always be "1".
			TorControlReply reply = await SendCommandAsync("PROTOCOLINFO 1\r\n", cancellationToken).ConfigureAwait(false);

			return ProtocolInfoReply.FromReply(reply);
		}

		/// <summary>
		/// Instructs Tor to shut down when this control connection is closed.
		/// </summary>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.23. TAKEOWNERSHIP.</seealso>
		public Task<TorControlReply> TakeOwnershipAsync(CancellationToken cancellationToken = default)
			=> SendCommandAsync("TAKEOWNERSHIP\r\n", cancellationToken);

		/// <summary>
		/// Requests controlled shutdown.
		/// </summary>
		/// <remarks>If server is a client (OP), exit immediately. If it's a relay (OR), close listeners and exit after <c>ShutdownWaitLength</c> seconds.</remarks>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.7. SIGNAL.</seealso>
		public Task<TorControlReply> SignalShutdownAsync(CancellationToken cancellationToken = default)
			=> SendCommandAsync("SIGNAL SHUTDOWN\r\n", cancellationToken);

		/// <summary>
		/// Causes Tor to stop polling for the existence of a process with its owning controller's PID.
		/// </summary>
		/// <remarks>If TAKEOWNERSHIP command is sent, Tor will still exit when the control connection ends.</remarks>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See 3.2. RESETCONF and 3.23. TAKEOWNERSHIP.</seealso>
		public Task<TorControlReply> ResetOwningControllerProcessConfAsync(CancellationToken cancellationToken = default)
			=> SendCommandAsync("RESETCONF __OwningControllerProcess\r\n", cancellationToken);

		/// <summary>
		/// Sends a command to Tor.
		/// </summary>
		/// <remarks>This is meant as a low-level API method, if needed for some reason.</remarks>
		/// <param name="command">A Tor control command which must end with <c>\r\n</c>.</param>
		public async Task<TorControlReply> SendCommandAsync(string command, CancellationToken cancellationToken = default)
		{
			Logger.LogTrace($"Client: About to send command: '{command.TrimEnd()}'");
			await PipeWriter.WriteAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(command)), cancellationToken).ConfigureAwait(false);
			await PipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

			TorControlReply reply = await TorControlReplyReader.ReadReplyAsync(PipeReader, cancellationToken).ConfigureAwait(false);

			return reply;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					TcpClient?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}
	}
}

using System;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using WalletWasabi.Tor.Control.Messages;
using WalletWasabi.Logging;

namespace WalletWasabi.Tor.Control
{
	public class TorControlClient : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public TorControlClient(TcpClient tcpClient)
		{
			TcpClient = tcpClient;

			PipeReader = PipeReader.Create(tcpClient.GetStream());
			PipeWriter = PipeWriter.Create(tcpClient.GetStream());
		}

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
		/// Sends a control command to Tor.
		/// </summary>
		/// <param name="command">A Tor control command which must end with <c>\r\n</c>.</param>
		public async Task<TorControlReply> SendCommandAsync(string command, CancellationToken cancellationToken = default)
		{
			Logger.LogDebug($"Client: About to send command: '{command.TrimEnd()}'");
			await PipeWriter.WriteAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(command)), cancellationToken).ConfigureAwait(false);
			await PipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
			Logger.LogTrace($"Client: Flushed write.");

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

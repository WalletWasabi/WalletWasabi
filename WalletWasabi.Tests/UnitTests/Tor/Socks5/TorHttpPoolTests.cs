using Moq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.Socks5
{
	/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
	[Collection("Serial unit tests collection")]
	public class TorHttpPoolTests
	{
		/// <summary>
		/// Transport stream implementation that behaves similarly to <see cref="NetworkStream"/>.
		/// <para>Writer can write to the stream multiple times, reader can read the written data.</para>
		/// </summary>
		/// <remarks>
		/// <see cref="MemoryStream"/> is not easy to use as a replacement for <see cref="NetworkStream"/> as we would need to use seek operation.
		/// </remarks>
		internal class TransportStream : IAsyncDisposable
		{
			public TransportStream(string testName)
			{
				// Construct unique pipe name.
				int n = new Random().Next(0, 1_000_000);
				string pipeName = $"{testName}.Pipe.{n}";

				Server = new(pipeName, PipeDirection.InOut, maxNumberOfServerInstances: 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
				Client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
			}

			public NamedPipeServerStream Server { get; }
			public NamedPipeClientStream Client { get; }

			public async Task ConnectAsync(CancellationToken cancellationToken)
			{
				Task connectClientTask = Server.WaitForConnectionAsync(cancellationToken);
				await Client.ConnectAsync(cancellationToken).ConfigureAwait(false);
				await connectClientTask.ConfigureAwait(false);
			}

			public async ValueTask DisposeAsync()
			{
				await Server.DisposeAsync();
				await Client.DisposeAsync();
			}
		}
	}
}

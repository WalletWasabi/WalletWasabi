using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tests.UnitTests.Tor.Control;

public static class PipeWriterExtensions
{
	public static ValueTask<FlushResult> WriteAsync(this PipeWriter writer, string data, Encoding encoding, CancellationToken cancellationToken = default)
	{
		return writer.WriteAsync(new ReadOnlyMemory<byte>(encoding.GetBytes(data)), cancellationToken);
	}

	public static async ValueTask WriteAsciiAndFlushAsync(this PipeWriter writer, string data, CancellationToken cancellationToken = default)
	{
		await writer.WriteAsync(new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(data)), cancellationToken).ConfigureAwait(false);
		await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
	}
}

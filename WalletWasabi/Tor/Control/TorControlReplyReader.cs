using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Control.Exceptions;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control
{
	public class TorControlReplyReader
	{
		/// <remarks>
		/// <see href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">Notes on an escaping bug</see> mentions that
		/// it is possible to implement a workaround so that a controller is compatible with buggy Tor implementations. This
		/// implementation DOES NOT do that.
		/// <para>However, such implementation is available <see href="https://github.com/zcash/zcash/pull/2251">here</see>.</para>
		/// </remarks>
		/// <exception cref="OperationCanceledException">When operation is canceled.</exception>
		/// <exception cref="IOException">When reading fails because the internal stream is forcibly closed, etc.</exception>
		/// <exception cref="TorControlReplyParseException">When received message does not follow Tor Control reply grammar.</exception>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section "4. Replies".</seealso>
		/// <seealso href="https://github.com/lontivero/Torino/blob/d891616777ed596ef54dbf1d86c9b4771e45e8f3/src/Reply.cs#L72"/>
		/// <seealso href="https://github.com/torproject/stem/blob/master/stem/response/__init__.py"/>
		public static async Task<TorControlReply> ReadReplyAsync(PipeReader reader, CancellationToken cancellationToken = default)
		{
			string line;

			try
			{
				line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (InvalidDataException e)
			{
				throw new TorControlReplyParseException("No reply line was received.", e);
			}

			if (line.Length < 3)
			{
				throw new TorControlReplyParseException("Status code requires at least 3 characters.");
			}

			if (!int.TryParse(line.AsSpan(0, 3), out int code))
			{
				throw new TorControlReplyParseException($"Unknown status code: '{line.Substring(0, 3)}'.");
			}

			line = line[3..];

			if (line.Length == 0)
			{
				return new TorControlReply((StatusCode)code, responseLines: new List<string>() { string.Empty });
			}

			char id = line[0];

			if (id != '+' && id != '-')
			{
				if (id == ' ')
				{
					line = line[1..];
				}

				return new TorControlReply((StatusCode)code, responseLines: new List<string>() { line });
			}

			List<string> responses = new();
			responses.Add(line[1..]);

			while (true)
			{
				line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

				if (line is null)
				{
					break;
				}

				if (line.Length == 0)
				{
					continue;
				}

				if (id == '-' && line.Length > 3 && line[3] == ' ')
				{
					responses.Add(line);
					break;
				}

				if (line.Length > 3 && id != '+')
				{
					line = line[4..];
				}

				responses.Add(line);

				if (id == '+' && ".".Equals(line, StringComparison.Ordinal))
				{
					line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
					responses.Add(line);
					break;
				}
			}

			return new TorControlReply((StatusCode)code, responseLines: responses);
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Control.Messages;

namespace WalletWasabi.Tor.Control
{
	/// <summary>
	/// TODO.
	/// </summary>
	public class TorControlReplyReader
	{
		/// <exception cref="OperationCanceledException"/>
		/// <seealso href="https://gitweb.torproject.org/torspec.git/tree/control-spec.txt">See section "4. Replies".</seealso>
		/// <seealso href="https://github.com/lontivero/Torino/blob/d891616777ed596ef54dbf1d86c9b4771e45e8f3/src/Reply.cs#L72"/>
		public static async Task<TorControlReply> ReadReplyAsync(PipeReader reader, CancellationToken cancellationToken = default)
		{
			Logger.LogTrace(">");

			try
			{
				string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

				if (line == null)
				{
					return new TorControlReply(StatusCode.Unknown);
				}

				if (line.Length < 3)
				{
					return new TorControlReply(StatusCode.Unknown);
				}

				if (!int.TryParse(line.AsSpan(0, 3), out int code))
				{
					return new TorControlReply(StatusCode.Unknown);
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

					if (id == '+' && ".".Equals(line))
					{
						break;
					}
				}

				return new TorControlReply((StatusCode)code, responses);
			}
			catch
			{
				return new TorControlReply(StatusCode.Unknown);
			}
		}
	}
}
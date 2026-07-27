using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// A child process with line-buffered stdout/stderr capture and marker-based waiting, modeled on
/// the stdout-marker pattern of payjoin-cli's own e2e tests and BTCPay's PayjoinCliPayer.
/// Proxy environment variables are scrubbed for every spawned process: the host sandbox proxy
/// (a host http_proxy on loopback) intercepts loopback HTTP and breaks the fixtures.
/// </summary>
public sealed class LineBufferedProcess : IDisposable
{
	private static readonly string[] ProxyEnvVars = ["http_proxy", "https_proxy", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "all_proxy"];

	private readonly Process _process;
	private readonly object _lock = new();
	private readonly List<string> _stdoutLines = [];
	private readonly List<string> _stderrLines = [];

	private LineBufferedProcess(Process process)
	{
		_process = process;
	}

	public bool HasExited => _process.HasExited;
	public int ExitCode => _process.ExitCode;

	public string StdoutText
	{
		get
		{
			lock (_lock)
			{
				return string.Join(Environment.NewLine, _stdoutLines);
			}
		}
	}

	public string StderrText
	{
		get
		{
			lock (_lock)
			{
				return string.Join(Environment.NewLine, _stderrLines);
			}
		}
	}

	public static LineBufferedProcess Start(string fileName, IEnumerable<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string>? environment = null)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = fileName,
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (string argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		foreach (string proxyVar in ProxyEnvVars)
		{
			startInfo.Environment.Remove(proxyVar);
		}

		startInfo.Environment["NO_PROXY"] = "127.0.0.1,localhost";
		startInfo.Environment["no_proxy"] = "127.0.0.1,localhost";

		if (environment is not null)
		{
			foreach ((string key, string value) in environment)
			{
				startInfo.Environment[key] = value;
			}
		}

		var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
		var result = new LineBufferedProcess(process);

		process.OutputDataReceived += (_, e) => result.Append(result._stdoutLines, e.Data);
		process.ErrorDataReceived += (_, e) => result.Append(result._stderrLines, e.Data);

		if (!process.Start())
		{
			throw new InvalidOperationException($"Failed to start '{fileName}'.");
		}

		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		return result;
	}

	/// <summary>Waits until any stdout line (past ones included) satisfies the predicate.</summary>
	public async Task<string> WaitForStdoutLineAsync(Func<string, bool> predicate, TimeSpan timeout, string markerDescription)
	{
		int scannedCount = 0;
		DateTime deadline = DateTime.UtcNow + timeout;

		while (true)
		{
			lock (_lock)
			{
				for (; scannedCount < _stdoutLines.Count; scannedCount++)
				{
					if (predicate(_stdoutLines[scannedCount]))
					{
						return _stdoutLines[scannedCount];
					}
				}
			}

			if (DateTime.UtcNow >= deadline)
			{
				throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0}s waiting for {markerDescription}.{DescribeBuffers()}");
			}

			if (HasExited)
			{
				// Give the async readers a moment to flush trailing output, then do a final scan.
				await Task.Delay(200).ConfigureAwait(false);
				lock (_lock)
				{
					for (; scannedCount < _stdoutLines.Count; scannedCount++)
					{
						if (predicate(_stdoutLines[scannedCount]))
						{
							return _stdoutLines[scannedCount];
						}
					}
				}

				throw new InvalidOperationException($"Process exited (code {ExitCode}) before emitting {markerDescription}.{DescribeBuffers()}");
			}

			await Task.Delay(50).ConfigureAwait(false);
		}
	}

	public async Task<int> WaitForExitAsync(TimeSpan timeout)
	{
		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			throw new TimeoutException($"Process did not exit within {timeout.TotalSeconds:0}s.{DescribeBuffers()}");
		}

		// Blocking overload waits for the redirected stream readers to drain.
		_process.WaitForExit();
		return _process.ExitCode;
	}

	public void Kill()
	{
		try
		{
			if (!_process.HasExited)
			{
				_process.Kill(entireProcessTree: true);
				_process.WaitForExit(5_000);
			}
		}
		catch (InvalidOperationException)
		{
			// The process finished in between; nothing to kill.
		}
	}

	public string DescribeBuffers()
	{
		var builder = new StringBuilder();
		builder.AppendLine();
		builder.AppendLine("---- stdout ----");
		builder.AppendLine(StdoutText);
		builder.AppendLine("---- stderr (tail) ----");
		string[] stderrLines;
		lock (_lock)
		{
			stderrLines = _stderrLines.ToArray();
		}

		builder.AppendLine(string.Join(Environment.NewLine, stderrLines.TakeLast(40)));
		return builder.ToString();
	}

	public void Dispose()
	{
		Kill();
		_process.Dispose();
	}

	private void Append(List<string> target, string? line)
	{
		if (line is null)
		{
			return;
		}

		lock (_lock)
		{
			target.Add(line);
		}
	}
}

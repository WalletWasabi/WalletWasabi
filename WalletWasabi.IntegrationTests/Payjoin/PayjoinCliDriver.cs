using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// Drives a payjoin-cli instance (one wallet role) against the harness fixtures, modeled on
/// BTCPay's PayjoinCliPayer: per-run working directory and sqlite database, generated
/// config.toml, RUST_LOG=debug, stdout-marker assertions. Unlike BTCPay's one-shot payer this
/// driver keeps handles to long-lived processes so tests can kill and resume sessions,
/// mirroring payjoin-cli's tests/e2e.rs choreography.
/// </summary>
public sealed partial class PayjoinCliDriver : IDisposable
{
	public const string PayjoinSentMarker = "Payjoin sent. TXID:";
	public const string NoResponseYetMarker = "No response yet.";
	public const string ResponseSuccessfulMarker = "Response successful";
	public const string SessionCompletedMarker = "Session completed.";
	public const string NoSessionsToResumeMarker = "No sessions to resume.";
	public const string SessionFailedMarker = "Session failed.";
	public const string FallbackBroadcastMarker = "Broadcasted fallback transaction txid";

	[GeneratedRegex("Payjoin sent\\. TXID: ([0-9a-f]{64})")]
	private static partial Regex SentTxidRegex();

	[GeneratedRegex("\\[(?:Sender|Receiver)\\s+([0-9a-f-]{36})\\]")]
	private static partial Regex SessionIdRegex();

	private readonly string _workDir;
	private readonly string _configPath;
	private readonly string? _rootCertificatePath;
	private readonly List<LineBufferedProcess> _processes = [];

	public PayjoinCliDriver(string workDir, string walletRpcUrl, string rpcUser, string rpcPassword, IReadOnlyList<string> ohttpRelayUrls, IReadOnlyList<string> pjDirectoryUrls, string? ohttpKeysPath = null, string? rootCertificatePath = null)
	{
		_workDir = workDir;
		Directory.CreateDirectory(_workDir);
		_configPath = Path.Combine(_workDir, "config.toml");

		// The root certificate must go on the command line: the config-file `root_certificate`
		// key is silently ignored at the pinned payjoin-cli (verified empirically; upstream
		// e2e only ever passes the flag). Requires a payjoin-cli built with _manual-tls.
		_rootCertificatePath = rootCertificatePath;

		var config = new StringBuilder();
		config.AppendLine(CultureInfo.InvariantCulture, $"db_path = \"{Path.Combine(_workDir, "payjoin.sqlite")}\"");
		config.AppendLine();
		config.AppendLine("[bitcoind]");
		config.AppendLine(CultureInfo.InvariantCulture, $"rpchost = \"{walletRpcUrl}\"");
		config.AppendLine(CultureInfo.InvariantCulture, $"rpcuser = \"{rpcUser}\"");
		config.AppendLine(CultureInfo.InvariantCulture, $"rpcpassword = \"{rpcPassword}\"");
		config.AppendLine();
		config.AppendLine("[v2]");
		config.AppendLine(CultureInfo.InvariantCulture, $"ohttp_relays = [{FormatTomlArray(ohttpRelayUrls)}]");
		config.AppendLine(CultureInfo.InvariantCulture, $"pj_directories = [{FormatTomlArray(pjDirectoryUrls)}]");
		if (ohttpKeysPath is not null)
		{
			config.AppendLine(CultureInfo.InvariantCulture, $"ohttp_keys = \"{ohttpKeysPath}\"");
		}

		File.WriteAllText(_configPath, config.ToString());
	}

	/// <summary>Starts a long-lived receive session; caller waits for the BIP21 URI or kills it mid-session.</summary>
	public LineBufferedProcess StartReceive(long amountSats, int expireInSeconds = 600)
	{
		return StartCli(["receive", amountSats.ToString(CultureInfo.InvariantCulture), "--expire-in", expireInSeconds.ToString(CultureInfo.InvariantCulture)]);
	}

	public LineBufferedProcess StartSend(string bip21, int feeRateSatsPerVb = 1)
	{
		return StartCli(["send", bip21, "--fee-rate", feeRateSatsPerVb.ToString(CultureInfo.InvariantCulture)]);
	}

	public LineBufferedProcess StartResume()
	{
		return StartCli(["resume"]);
	}

	/// <summary>Cancels a session, broadcasting the original (fallback) transaction by default.</summary>
	public LineBufferedProcess StartCancel(string sessionId)
	{
		return StartCli(["cancel", sessionId]);
	}

	public static async Task<string> WaitForBip21Async(LineBufferedProcess receiveProcess)
	{
		string line = await receiveProcess.WaitForStdoutLineAsync(
			l => l.TrimStart().StartsWith("bitcoin:", StringComparison.OrdinalIgnoreCase),
			TimeSpan.FromSeconds(30),
			"BIP21 payjoin URI").ConfigureAwait(false);
		return line.Trim();
	}

	public static string ParseSessionId(string stdout)
	{
		Match match = SessionIdRegex().Match(stdout);
		if (!match.Success)
		{
			throw new InvalidOperationException($"No session id found in payjoin-cli output:{Environment.NewLine}{stdout}");
		}

		return match.Groups[1].Value;
	}

	public static string ParseSentTxid(string stdout)
	{
		Match match = SentTxidRegex().Match(stdout);
		if (!match.Success)
		{
			throw new InvalidOperationException($"No '{PayjoinSentMarker}' marker in payjoin-cli output:{Environment.NewLine}{stdout}");
		}

		return match.Groups[1].Value;
	}

	public void Dispose()
	{
		foreach (LineBufferedProcess process in _processes)
		{
			process.Dispose();
		}

		try
		{
			if (Directory.Exists(_workDir))
			{
				Directory.Delete(_workDir, recursive: true);
			}
		}
		catch (IOException)
		{
			// Best-effort temp cleanup.
		}
	}

	private LineBufferedProcess StartCli(IEnumerable<string> arguments)
	{
		IEnumerable<string> fullArguments = _rootCertificatePath is null
			? arguments
			: ["--root-certificate", _rootCertificatePath, .. arguments];

		// The cli reads config.toml from its working directory; RUST_LOG=debug for diagnostics on failure.
		LineBufferedProcess process = LineBufferedProcess.Start(
			HarnessBinaries.PayjoinCliPath,
			fullArguments,
			_workDir,
			new Dictionary<string, string> { ["RUST_LOG"] = "debug" });
		_processes.Add(process);
		return process;
	}

	private static string FormatTomlArray(IReadOnlyList<string> values)
	{
		return string.Join(", ", values.Select(v => $"\"{v}\""));
	}
}

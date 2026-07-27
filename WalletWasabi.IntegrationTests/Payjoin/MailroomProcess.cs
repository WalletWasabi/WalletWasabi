using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// A standalone payjoin-mailroom process serving plain HTTP on an ephemeral loopback port.
/// One mailroom instance serves both the directory and OHTTP relay roles, but relay→directory
/// requests within a single instance are rejected as self-loops (shared sentinel tag), so a
/// harness needs one instance per role — the same topology payjoin-test-utils' TestServices
/// builds in-process. The standalone binary cannot serve TLS (serve_manual_tls is a library
/// function behind the _manual-tls feature that main() never reaches), hence plain HTTP.
/// </summary>
public sealed partial class MailroomProcess : IDisposable
{
	/// <summary>Directory long-poll hold; payjoin clients re-poll immediately, so this bounds test latency.</summary>
	private const int LongPollTimeoutSeconds = 2;

	[GeneratedRegex(@"listening on Ok\(Tcp\(127\.0\.0\.1:(\d+)")]
	private static partial Regex ListeningPortRegex();

	private readonly LineBufferedProcess _process;

	private MailroomProcess(LineBufferedProcess process, int port, string workDir)
	{
		_process = process;
		Port = port;
		WorkDir = workDir;
	}

	public int Port { get; }
	public string WorkDir { get; }
	public string Url => $"http://127.0.0.1:{Port}";

	/// <param name="enableV1">Enable the BIP78 fallback endpoints; the directory role wants this, relays do not need it.</param>
	public static async Task<MailroomProcess> StartAsync(string workDir, bool enableV1, HttpClient httpClient)
	{
		Directory.CreateDirectory(workDir);
		string storageDir = Path.Combine(workDir, "data");
		Directory.CreateDirectory(storageDir);

		string config = $"""
			listener = "127.0.0.1:0"
			storage_dir = "{storageDir}"
			timeout = {LongPollTimeoutSeconds}
			mailbox_ttl = 3600
			{(enableV1 ? "[v1]" : "")}
			""";
		string configPath = Path.Combine(workDir, "config.toml");
		await File.WriteAllTextAsync(configPath, config).ConfigureAwait(false);

		LineBufferedProcess process = LineBufferedProcess.Start(
			HarnessBinaries.MailroomPath,
			["--config", configPath],
			workDir);

		try
		{
			string listeningLine = await process.WaitForStdoutLineAsync(
				line => ListeningPortRegex().IsMatch(line),
				TimeSpan.FromSeconds(20),
				"payjoin-mailroom 'listening on' line").ConfigureAwait(false);

			int port = int.Parse(ListeningPortRegex().Match(listeningLine).Groups[1].Value);
			var mailroom = new MailroomProcess(process, port, workDir);

			using HttpResponseMessage health = await httpClient.GetAsync($"{mailroom.Url}/health").ConfigureAwait(false);
			health.EnsureSuccessStatusCode();
			return mailroom;
		}
		catch
		{
			process.Dispose();
			throw;
		}
	}

	/// <summary>Fetches the directory's OHTTP key configuration without going through a relay.</summary>
	/// <remarks>
	/// Test-only shortcut. The production bootstrap path (RFC 9540 fetch proxied through a relay)
	/// only works via CONNECT/WebSocket tunneling to an https gateway; over plain HTTP the relay
	/// answers proxy-form GETs from its own combined-binary directory service, returning the wrong
	/// keys. payjoin-cli's own e2e pre-fetches keys to a file the same way.
	/// </remarks>
	public async Task<string> FetchOhttpKeysAsync(string destinationPath, HttpClient httpClient)
	{
		byte[] keys = await httpClient.GetByteArrayAsync($"{Url}/ohttp-keys").ConfigureAwait(false);
		await File.WriteAllBytesAsync(destinationPath, keys).ConfigureAwait(false);
		return destinationPath;
	}

	public void Dispose()
	{
		_process.Dispose();
	}
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// The contrib/payjoin-fixture shim: payjoin-test-utils' TestServices as a child process —
/// a payjoin-mailroom directory served over self-signed TLS plus N plain-HTTP OHTTP relay
/// instances whose outgoing clients trust that certificate. This is the TLS-fidelity topology
/// (production payjo.in is https); the plain-HTTP MailroomProcess topology remains as the
/// degradation path if the shim rots.
/// </summary>
public sealed class PayjoinTestServicesProcess : IDisposable
{
	private readonly LineBufferedProcess _process;

	private PayjoinTestServicesProcess(LineBufferedProcess process, string directoryUrl, string[] relayUrls, byte[] certificateDer, string certificatePath)
	{
		_process = process;
		DirectoryUrl = directoryUrl;
		RelayUrls = relayUrls;
		CertificateDer = certificateDer;
		CertificatePath = certificatePath;
	}

	public string DirectoryUrl { get; }
	public string[] RelayUrls { get; }
	public byte[] CertificateDer { get; }

	/// <summary>DER file on disk, handed to payjoin-cli as its <c>root_certificate</c>.</summary>
	public string CertificatePath { get; }

	public static async Task<PayjoinTestServicesProcess> StartAsync(string workDir, int relays = 2)
	{
		Directory.CreateDirectory(workDir);
		string certPath = Path.Combine(workDir, "cert.der");

		LineBufferedProcess process = LineBufferedProcess.Start(
			HarnessBinaries.PayjoinFixturePath,
			["--relays", relays.ToString(System.Globalization.CultureInfo.InvariantCulture), "--cert-out", certPath],
			workDir);

		try
		{
			// The shim only prints READY after TestServices' own health checks pass.
			await process.WaitForStdoutLineAsync(line => line == "READY", TimeSpan.FromSeconds(60), "payjoin-fixture READY line").ConfigureAwait(false);

			string stdout = process.StdoutText;
			string directoryUrl = ParseValue(stdout, "DIRECTORY_URL");
			string[] relayUrls = ParseValue(stdout, "OHTTP_RELAY_URLS").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			byte[] certDer = await File.ReadAllBytesAsync(certPath).ConfigureAwait(false);

			return new PayjoinTestServicesProcess(process, directoryUrl, relayUrls, certDer, certPath);
		}
		catch
		{
			process.Dispose();
			throw;
		}
	}

	public void Dispose()
	{
		_process.Dispose();
	}

	private static string ParseValue(string stdout, string key)
	{
		string prefix = key + "=";
		string? line = stdout.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));
		return line?[prefix.Length..] ?? throw new InvalidOperationException($"payjoin-fixture did not print {key}.{Environment.NewLine}{stdout}");
	}
}

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using WalletWasabi.Bases;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tor.StatusChecker;

/// <summary>
/// Component that periodically checks https://status.torproject.org/ to detect network disruptions.
/// </summary>
public class TorStatusChecker : PeriodicRunner
{
	private static readonly Uri TorStatusUri = new("https://status.torproject.org/index.xml");
	private static readonly XmlSerializer StatusReportXmlSerializer = new(typeof(StatusReport));

	public TorStatusChecker(TimeSpan period, IHttpClient httpClient)
		: base(period)
	{
		HttpClient = httpClient;
	}

	public event EventHandler<StatusReport[]>? StatusEvent;
	private IHttpClient HttpClient { get; }

	/// <inheritdoc/>
	protected override async Task ActionAsync(CancellationToken cancellationToken)
	{
		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, TorStatusUri);
			using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

			string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			StatusReport? report = ParseFirstStatusReport(html);

			// Fire event.
			if (report is not null)
			{
				StatusEvent?.Invoke(this, new StatusReport[] { report });
			}
		}
		catch (Exception ex)
		{
			Logger.LogDebug("Failed to get/parse Tor status page.", ex);
		}
	}

	internal static StatusReport? ParseFirstStatusReport(string xml)
	{
		XmlDocument document = new();
		document.LoadXml(xml);
		XmlNode? node = document.SelectSingleNode("//rss/channel/item");

		StatusReport? result = null;

		if (node is not null)
		{
			using XmlNodeReader reader = new(node);
			result = (StatusReport?)StatusReportXmlSerializer.Deserialize(reader);
		}

		return result;
	}
}

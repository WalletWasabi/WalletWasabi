using System.Linq;
using WalletWasabi.Tor.StatusChecker;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services.Tor;

/// <summary>
/// Tests for <see cref="XmlIssueListParser"/>
/// </summary>
public class XmlIssueListParserTests
{
	/// <summary>
	/// Shortened sample content of https://status.torproject.org/index.xml.
	/// </summary>
	private static string StatusXmlSample => @"
<rss version=""2.0"" xmlns:atom=""http://www.w3.org/2005/Atom"">
    <channel>

      <title>Tor Project status</title>
      <link>https://status.torproject.org/</link>
      <description>Incident history</description>
      <generator>github.com/cstate</generator>
      <language>en</language>

      <lastBuildDate>Thu, 09 Jun 2022 14:00:00 +0000</lastBuildDate>

        <atom:link href=""https://status.torproject.org/index.xml"" rel=""self"" type=""application/rss+xml"" />

        <item>
          <title>Network DDoS</title>
          <link>https://status.torproject.org/issues/2022-06-09-network-ddos/</link>
          <pubDate>Thu, 09 Jun 2022 14:00:00 +0000</pubDate>
          <guid>https://status.torproject.org/issues/2022-06-09-network-ddos/</guid>
          <category></category>
          <description>&lt;p&gt;We are experiencing a network-wide DDoS attempt impacting the
performance of the Tor network, which includes both onion services and
non-onion services traffic. We are currently investigating potential
mitigations.&lt;/p&gt;
</description>
        </item>

        <item>
          <title>[Resolved] routing issues at main provider</title>
          <link>https://status.torproject.org/issues/2022-01-25-routing-issues/</link>
          <pubDate>Tue, 25 Jan 2022 06:00:00 +0000</pubDate>
          <guid>https://status.torproject.org/issues/2022-01-25-routing-issues/</guid>
          <category>2022-01-27 18:58:00 &#43;0000</category>
          <description>&lt;p&gt;We are experiencing intermittent network outages that typically
resolve themselves within a few hours. Preliminary investigations seem
to point at routing issues at Hetzner, but we have yet to get a solid
diagnostic. We&amp;rsquo;re following that issue in &lt;a href=&#34;https://gitlab.torproject.org/tpo/tpa/team/-/issues/40601&#34;&gt;issue 40601&lt;/a&gt;.&lt;/p&gt;
</description>
        </item>

        <item>
          <title>[Resolved] Disruption of Metrics website and relay search</title>
          <link>https://status.torproject.org/issues/2021-06-05-metris-website/</link>
          <pubDate>Thu, 03 Jun 2021 08:00:00 +0000</pubDate>
          <guid>https://status.torproject.org/issues/2021-06-05-metris-website/</guid>
          <category>2021-06-14 16:00:00</category>
          <description>&lt;p&gt;We&amp;rsquo;re currently facing stability issues with respect to our Metrics website and relay search. We are actively &lt;a href=&#34;https://gitlab.torproject.org/tpo/metrics/team/-/issues/15&#34;&gt;working on resolving those issues&lt;/a&gt;
as soon as possible.&lt;/p&gt;
</description>
        </item>
    </channel>
  </rss>
";

	/// <summary>
	/// Checks that we can parse and obtain the first <see cref="Issue"/> from the sample XML in <see cref="StatusXmlSample"/>.
	/// </summary>
	[Fact]
	public void Parse_and_get_first_issue()
	{
		var issue = new XmlIssueListParser()
			.Parse(StatusXmlSample)
			.FirstOrDefault();

		Assert.NotNull(issue);
		Assert.Equal("Network DDoS", issue!.Title);
		Assert.False(issue!.Resolved);
	}
}

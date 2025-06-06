using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Serialization;
using WalletWasabi.Tor.StatusChecker;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services.Tor;
public class TorStatusCheckerTests
{
	[Fact]
	public void DecodeTorStatusResponseWithNoIssues()
	{
		string jsonResponseWithNoIssue = """
			{
			  "is": "index",
			  "cStateVersion": "6.0",
			  "apiVersion": "2.0",
			  "title": "Tor Project status",
			  "languageCodeHTML": "en",
			  "languageCode": "en",
			  "baseURL": "https://status.torproject.org/",
			  "description": "We continuously monitor the status of our services and if there are any interruptions an update will be posted here. If you need to modify this page, [follow the documentation](https://gitlab.torproject.org/tpo/tpa/team/-/wikis/service/status) ([mirror](https://web.archive.org/web/2/https://gitlab.torproject.org/tpo/tpa/team/-/wikis/service/status)).",
			  "summaryStatus": "disrupted",
			  "categories": [
			      {
			        "name": "Tor Network",
			        "description": "Core services that are integral to the Tor Network.",
			        "hideTitle": false,
			        "closedByDefault": false
			      }
			    ,
			      {
			        "name": "Tor Project websites",
			        "description": "Official websites and services hosted and maintained by the Tor Project.",
			        "hideTitle": false,
			        "closedByDefault": false
			      }
			    ,
			      {
			        "name": "Internal systems",
			        "description": "Tools and services that help us collaborate with one another, managed by the Tor Project System Administration Team (TPA).",
			        "hideTitle": false,
			        "closedByDefault": true
			      }
			    ,
			      {
			        "name": "Deprecated",
			        "description": "Systems here are deprecated and kept for historical purposes",
			        "hideTitle": true,
			        "closedByDefault": true
			      }
			    ,
			      {
			        "name": "Uncategorized",
			        "hideTitle": true,
			        "closedByDefault": false
			      }

			  ],


			  "pinnedIssues": [],
			  "systems": [

			      {
			        "name": "v3 Onion Services",
			        "description": "Third generation onion services – running private services (e.g. websites) that are only accessible through the Tor network.",
			        "category": "Tor Network",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "v2 Onion Services",
			        "description": "Second generation onion services – running private services (e.g. websites) that are only accessible through the Tor network.",
			        "category": "Deprecated",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Directory Authorities",
			        "description": "Special-purpose relays that maintain a list of currently-running relays and periodically publish a consensus together.",
			        "category": "Tor Network",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Network Experiments",
			        "description": "Experiments running on the Tor network.",
			        "category": "Tor Network",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "torproject.org",
			        "description": "Our official website and its associated portals (e.g. community.torproject.org).",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Tor Check",
			        "description": "The check.torproject.org website, used to confirm you're connected to the Tor Network.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "metrics.torproject.org",
			        "description": "The metrics.torproject.org website, used to easily access metrics related to Tor.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []
			      },
			      {
			        "name": "ExoneraTor",
			        "description": "Queryable database of IP addresses which were a Tor relay at a specified date.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "weather.torproject.org",
			        "description": "The weather.torproject.org website, providing a service for relay operators to keep track of their relays.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "donate.torproject.org",
			        "description": "The donate.torproject.org website, used to donate funds to the Tor Project.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "deb.torproject.org",
			        "description": "The Debian archive",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "GitLab",
			        "description": "Our code-review, bug tracking, and collaboration platform of choice (gitlab.torproject.org).",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Email",
			        "description": "General email delivery from within the Tor Project.",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Mailing lists",
			        "description": "Our public and private mailing lists (lists.torproject.org) powered by Mailman, the GNU Mailing List Manager.",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "DNS",
			        "description": "Domain name resolution for the Tor Project infrastructure.",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      }
			  ],


			  "buildDate": "2025-05-01",
			  "buildTime": "15:22",
			  "buildTimezone": "UTC",
			  "colorBrand": "#7D4698",
			  "colorOk": "#158474",
			  "colorDisrupted": "#B35E0F",
			  "colorDown": "#CA3D46",
			  "colorNotice": "#157BB2",
			  "alwaysKeepBrandColor": "true",
			  "logo": "https://status.torproject.org/logo.png",
			  "googleAnalytics": "UA-00000000-1"
			}
			""";

		var deserialized = JsonDecoder.FromString(jsonResponseWithNoIssue, Decode.TorStatus);
		Assert.NotNull(deserialized);
		Assert.NotEmpty(deserialized.Systems);
		Assert.NotEmpty(deserialized.Systems.Where(sys => new[]{ "v3 Onion Services", "Directory Authorities", "DNS" }.Contains(sys.Name)));
		Assert.All(deserialized.Systems, sys => Assert.Equal("ok", sys.Status));
		Assert.All(deserialized.Systems, sys => Assert.Empty(sys.UnresolvedIssues));

	}

	[Fact]
	public void DecodeTorStatusResponseWithIssues()
	{
		string jsonResponseWithIssues = """
			{
			  "is": "index",
			  "cStateVersion": "6.0",
			  "apiVersion": "2.0",
			  "title": "Tor Project status",
			  "languageCodeHTML": "en",
			  "languageCode": "en",
			  "baseURL": "https://status.torproject.org/",
			  "description": "We continuously monitor the status of our services and if there are any interruptions an update will be posted here. If you need to modify this page, [follow the documentation](https://gitlab.torproject.org/tpo/tpa/team/-/wikis/service/status) ([mirror](https://web.archive.org/web/2/https://gitlab.torproject.org/tpo/tpa/team/-/wikis/service/status)).",
			  "summaryStatus": "disrupted",
			  "categories": [
			      {
			        "name": "Tor Network",
			        "description": "Core services that are integral to the Tor Network.",
			        "hideTitle": false,
			        "closedByDefault": false
			      }
			    ,
			      {
			        "name": "Tor Project websites",
			        "description": "Official websites and services hosted and maintained by the Tor Project.",
			        "hideTitle": false,
			        "closedByDefault": false
			      }
			    ,
			      {
			        "name": "Internal systems",
			        "description": "Tools and services that help us collaborate with one another, managed by the Tor Project System Administration Team (TPA).",
			        "hideTitle": false,
			        "closedByDefault": true
			      }
			    ,
			      {
			        "name": "Deprecated",
			        "description": "Systems here are deprecated and kept for historical purposes",
			        "hideTitle": true,
			        "closedByDefault": true
			      }
			    ,
			      {
			        "name": "Uncategorized",
			        "hideTitle": true,
			        "closedByDefault": false
			      }

			  ],


			  "pinnedIssues": [],
			  "systems": [

			      {
			        "name": "v3 Onion Services",
			        "description": "Third generation onion services – running private services (e.g. websites) that are only accessible through the Tor network.",
			        "category": "Tor Network",

			        "status": "disrupted",

			        "unresolvedIssues": [
					{
					  "is": "issue",
					  "title": "Made up problem with v3 Onion Service",
					  "createdAt": "2025-04-10 13:00:00 +0000 UTC",
					  "lastMod": "2025-04-10 13:47:08 +0000 UTC",
					  "permalink": "https://status.torproject.org/issues/2025-04-10-bridgedb-graphs-on-metrics-website/",
					  "severity": "disrupted",
					  "resolved": false,
					  "informational": false,
					  "resolvedAt": "<no value>",
					  "affected": ["metrics.torproject.org"],
					  "filename": "2025-04-10-bridgedb-graphs-on-metrics-website.md"
					}

					]

			      },
			      {
			        "name": "v2 Onion Services",
			        "description": "Second generation onion services – running private services (e.g. websites) that are only accessible through the Tor network.",
			        "category": "Deprecated",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Directory Authorities",
			        "description": "Special-purpose relays that maintain a list of currently-running relays and periodically publish a consensus together.",
			        "category": "Tor Network",

			        "status": "disrupted",

			        "unresolvedIssues": [
					{
					  "is": "issue",
					  "title": "Test issue with Directory Authorities",
					  "createdAt": "2025-04-10 13:00:00 +0000 UTC",
					  "lastMod": "2025-04-10 13:47:08 +0000 UTC",
					  "permalink": "https://status.torproject.org/issues/2025-04-10-bridgedb-graphs-on-metrics-website/",
					  "severity": "disrupted",
					  "resolved": false,
					  "informational": false,
					  "resolvedAt": "<no value>",
					  "affected": ["metrics.torproject.org"],
					  "filename": "2025-04-10-bridgedb-graphs-on-metrics-website.md"
					}

					]

			      },
			      {
			        "name": "Network Experiments",
			        "description": "Experiments running on the Tor network.",
			        "category": "Tor Network",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "torproject.org",
			        "description": "Our official website and its associated portals (e.g. community.torproject.org).",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Tor Check",
			        "description": "The check.torproject.org website, used to confirm you're connected to the Tor Network.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "metrics.torproject.org",
			        "description": "The metrics.torproject.org website, used to easily access metrics related to Tor.",
			        "category": "Tor Project websites",

			        "status": "disrupted",

			        "unresolvedIssues": [
					{
					  "is": "issue",
					  "title": "BridgeDB graphs on Metrics website are not shown",
					  "createdAt": "2025-04-10 13:00:00 +0000 UTC",
					  "lastMod": "2025-04-10 13:47:08 +0000 UTC",
					  "permalink": "https://status.torproject.org/issues/2025-04-10-bridgedb-graphs-on-metrics-website/",
					  "severity": "disrupted",
					  "resolved": false,
					  "informational": false,
					  "resolvedAt": "<no value>",
					  "affected": ["metrics.torproject.org"],
					  "filename": "2025-04-10-bridgedb-graphs-on-metrics-website.md"
					}

					]
			      },
			      {
			        "name": "ExoneraTor",
			        "description": "Queryable database of IP addresses which were a Tor relay at a specified date.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "weather.torproject.org",
			        "description": "The weather.torproject.org website, providing a service for relay operators to keep track of their relays.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "donate.torproject.org",
			        "description": "The donate.torproject.org website, used to donate funds to the Tor Project.",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "deb.torproject.org",
			        "description": "The Debian archive",
			        "category": "Tor Project websites",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "GitLab",
			        "description": "Our code-review, bug tracking, and collaboration platform of choice (gitlab.torproject.org).",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Email",
			        "description": "General email delivery from within the Tor Project.",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "Mailing lists",
			        "description": "Our public and private mailing lists (lists.torproject.org) powered by Mailman, the GNU Mailing List Manager.",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      },
			      {
			        "name": "DNS",
			        "description": "Domain name resolution for the Tor Project infrastructure.",
			        "category": "Internal systems",

			        "status": "ok",

			        "unresolvedIssues": []

			      }
			  ],


			  "buildDate": "2025-05-01",
			  "buildTime": "15:22",
			  "buildTimezone": "UTC",
			  "colorBrand": "#7D4698",
			  "colorOk": "#158474",
			  "colorDisrupted": "#B35E0F",
			  "colorDown": "#CA3D46",
			  "colorNotice": "#157BB2",
			  "alwaysKeepBrandColor": "true",
			  "logo": "https://status.torproject.org/logo.png",
			  "googleAnalytics": "UA-00000000-1"
			}
			""";

		var deserialized = JsonDecoder.FromString(jsonResponseWithIssues, Decode.TorStatus);
		Assert.NotNull(deserialized);
		Assert.NotEmpty(deserialized.Systems);

		var v3Service = deserialized.Systems.First(sys => sys.Name == "v3 Onion Services");
		Assert.NotNull(v3Service);
		Assert.Equal("disrupted", v3Service.Status);
		Assert.NotEmpty(v3Service.UnresolvedIssues);
		Assert.NotNull(v3Service.UnresolvedIssues.First().Title);
	}
}

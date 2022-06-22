using System.Linq;
using WalletWasabi.Tor.NetworkChecker;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor.NetworkChecker;

public class IssueParserTests
{
	[Fact]
	public void Parse()
	{
		var toParse = @"---
title: Network DDoS
date: 2022-06-09 14:00:00
resolved: false
# Possible severity levels: down, disrupted, notice
severity: disrupted
affected:
  - v3 Onion Services
section: issue
---

We are experiencing a network-wide DDoS attempt impacting the
performance of the Tor network, which includes both onion services and
non-onion services traffic. We are currently investigating potential
mitigations.";
		var sut = new IssueParser();

		var issue = sut.Parse(toParse);

		Assert.Equal("Network DDoS", issue.Title);
		Assert.Equal(DateTimeOffset.Parse("2022-06-09 14:00:00"), issue.Date);
		Assert.Equal("disrupted", issue.Severity);
		Assert.True(new[] { "v3 Onion Services" }.SequenceEqual(issue.Affected));
	}
}

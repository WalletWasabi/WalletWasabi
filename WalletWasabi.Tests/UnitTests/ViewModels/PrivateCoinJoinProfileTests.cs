using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using Xunit;
namespace WalletWasabi.Tests.UnitTests.ViewModels;

public class PrivacyCoinJoinProfileTests
{
	[Fact]
	public void AddressPropertiesAreExposedCorrectly()
	{
		var minExclusive = PrivateCoinJoinProfileViewModel.MinAnonScore;
		var maxExclusive = PrivateCoinJoinProfileViewModel.MaxAnonScore;

		List<int> results = [];
		Enumerable.Range(0, 10_000).ToList().ForEach(_ => results.Add(PrivateCoinJoinProfileViewModel.GetAnonScoreTarget(minExclusive, maxExclusive)));

		Assert.True(results.Min() > minExclusive);
		Assert.True(results.Max() < maxExclusive);

		double sanityLimitAverage = minExclusive + (maxExclusive - minExclusive) * (1.0/3.0);
		double actualAverage = results.Average();

		Assert.True(actualAverage <= sanityLimitAverage);
	}
}

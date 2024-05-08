using NBitcoin;
using System.Collections.Generic;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client;

public class CoinjoinSkipFactorsTests
{
	[Fact]
	public void ShouldNotSkipRoundRandomly()
	{
		var random = new InsecureRandom();
		var roundFeeRate = new FeeRate(2m);
		var coinJoinFeeRateMedians = new Dictionary<TimeSpan, FeeRate>
		{
			{ TimeSpan.FromHours(24), new FeeRate(1m) },
			{ TimeSpan.FromHours(168), new FeeRate(1m) },
			{ TimeSpan.FromHours(720), new FeeRate(1m) }
		};

		// Under no circumstances should you skip randomly for 1, 1, 1 or NoSkip, despite high fee rate.
		for (int i = 0; i < 100; i++)
		{
			var factors = new CoinjoinSkipFactors(1, 1, 1);
			Assert.False(factors.ShouldSkipRoundRandomly(random, roundFeeRate, coinJoinFeeRateMedians));

			factors = CoinjoinSkipFactors.NoSkip;
			Assert.False(factors.ShouldSkipRoundRandomly(random, roundFeeRate, coinJoinFeeRateMedians));
		}

		roundFeeRate = new FeeRate(1m);
		coinJoinFeeRateMedians = new Dictionary<TimeSpan, FeeRate>
		{
			{ TimeSpan.FromHours(24), new FeeRate(2m) },
			{ TimeSpan.FromHours(168), new FeeRate(2m) },
			{ TimeSpan.FromHours(720), new FeeRate(2m) }
		};

		// Under no circumstances should you skip randomly ever when fee rate is lower than medians.
		for (int i = 0; i < 100; i++)
		{
			var factors = new CoinjoinSkipFactors(0, 0, 0);
			Assert.False(factors.ShouldSkipRoundRandomly(random, roundFeeRate, coinJoinFeeRateMedians));

			factors = CoinjoinSkipFactors.CostMinimizing;
			Assert.False(factors.ShouldSkipRoundRandomly(random, roundFeeRate, coinJoinFeeRateMedians));

			factors = CoinjoinSkipFactors.SpeedMaximizing;
			Assert.False(factors.ShouldSkipRoundRandomly(random, roundFeeRate, coinJoinFeeRateMedians));

			factors = CoinjoinSkipFactors.PrivacyMaximizing;
			Assert.False(factors.ShouldSkipRoundRandomly(random, roundFeeRate, coinJoinFeeRateMedians));
		}
	}

	[Fact]
	public void ShouldSkipRoundRandomly()
	{
		var random = new InsecureRandom();
		var roundFeeRate = new FeeRate(2m);
		var coinJoinFeeRateMedians = new Dictionary<TimeSpan, FeeRate>
		{
			{ TimeSpan.FromHours(24), new FeeRate(1m) },
			{ TimeSpan.FromHours(168), new FeeRate(1m) },
			{ TimeSpan.FromHours(720), new FeeRate(1m) }
		};

		// When fee rate is high and factors are 0, only then skipping is inevitable.
		for (int i = 0; i < 100; i++)
		{
			var factors = new CoinjoinSkipFactors(0, 0, 0);
			Assert.True(factors.ShouldSkipRoundRandomly(random, roundFeeRate, coinJoinFeeRateMedians));
		}
	}

	[Theory]
	[InlineData("1_1_1", 1, 1, 1)]
	[InlineData("0.7_0.8_0.9", 0.7, 0.8, 0.9)]
	[InlineData("0.1_0.2_0.3", 0.1, 0.2, 0.3)]
	[InlineData("0.5_0.5_0.5", 0.5, 0.5, 0.5)]
	[InlineData("0.1_123.3_7.3", 0.1, 1, 1)]
	[InlineData("-0.1_-123.3_7.3", 0, 0, 1)]
	public void FromStringTest(string skipFactorStr, double daily, double weekly, double monthly)
	{
		var testString = $"{skipFactorStr}";
		var result = CoinjoinSkipFactors.FromString(testString);

		Assert.Equal(daily, result.Daily);
		Assert.Equal(weekly, result.Weekly);
		Assert.Equal(monthly, result.Monthly);
	}

	[Theory]
	[InlineData("1.0_1.0_1.0", 1, 1, 1)]
	[InlineData("1.5_2.8_3.1", 1, 1, 1)]
	[InlineData("0.5_0.8_1.1", 0.5, 0.8, 1)]
	public void ToStringTest(string skipFactorStr, double daily, double weekly, double monthly)
	{
		var testString = $"{skipFactorStr}";
		var result = CoinjoinSkipFactors.FromString(testString);

		Assert.Equal(daily, result.Daily);
		Assert.Equal(weekly, result.Weekly);
		Assert.Equal(monthly, result.Monthly);
	}

	[Theory]
	[InlineData(0.1, 0.2, 0.3, 0.1, 0.2, 0.3, true)]
	[InlineData(1.1, 1.2, 13, 13, 1.3, 1.4, true)]
	[InlineData(-1.1, -1.2, -13, -13, -1.3, -1.4, true)]
	[InlineData(0.1, 0.2, 0.3, 0.1, 0.2, 0.9, false)]
	[InlineData(0.1, 0.2, 0.3, 0.1, 0.9, 0.3, false)]
	[InlineData(0.1, 0.2, 0.3, 0.9, 0.2, 0.3, false)]
	public void EqualsTest(double daily1, double weekly1, double monthly1, double daily2, double weekly2, double monthly2, bool expected)
	{
		// Arrange
		var factor1 = new CoinjoinSkipFactors(daily1, weekly1, monthly1);
		var factor2 = new CoinjoinSkipFactors(daily2, weekly2, monthly2);

		// Assert
		Assert.Equal(expected, factor1.Equals(factor2));
		Assert.Equal(expected, factor2.Equals(factor1));
		Assert.Equal(expected, factor1 == factor2);
		Assert.Equal(!expected, factor1 != factor2);
		Assert.Equal(expected, factor1.GetHashCode() == factor2.GetHashCode());
	}
}

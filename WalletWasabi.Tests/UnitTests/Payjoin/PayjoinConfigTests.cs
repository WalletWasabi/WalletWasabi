using WalletWasabi.Client.Configuration;
using WalletWasabi.Payjoin;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Payjoin;

/// <summary>
/// The receiver fee-rate cap is a config override knob: it defaults to a conservative
/// safety bound (<see cref="PayjoinConstants.DefaultMaxFeeRateSatPerVb"/>) and an operator
/// can raise or lower it via CLI argument or the matching WASABI_ environment variable.
/// </summary>
public class PayjoinConfigTests
{
	private static Config Build(params string[] cliArgs) =>
		new(PersistentConfigManager.DefaultMainNetConfig, cliArgs);

	[Fact]
	public void DefaultResolvesTo250()
	{
		Config config = Build();

		Assert.Equal(250ul, config.PayjoinMaxFeeRateSatPerVb);
		Assert.Equal(PayjoinConstants.DefaultMaxFeeRateSatPerVb, config.PayjoinMaxFeeRateSatPerVb);
	}

	[Fact]
	public void CliArgumentOverridesDefault()
	{
		Config config = Build("--PayjoinMaxFeeRateSatPerVb=100");

		Assert.Equal(100ul, config.PayjoinMaxFeeRateSatPerVb);
		Assert.True(config.IsOverridden);
	}

	[Fact]
	public void CliArgumentIsCaseInsensitive()
	{
		Config config = Build("--payjoinmaxfeeratesatpervb=1000");

		Assert.Equal(1000ul, config.PayjoinMaxFeeRateSatPerVb);
	}

	[Fact]
	public void NonNumericOverrideThrows()
	{
		Assert.Throws<System.ArgumentException>(() => Build("--PayjoinMaxFeeRateSatPerVb=notanumber"));
	}
}

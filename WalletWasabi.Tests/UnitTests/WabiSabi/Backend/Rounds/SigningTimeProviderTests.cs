using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Rounds;

public class SigningTimeProviderTests
{
	[Fact]
	public void TrezorSigningTimeProviderTest()
	{
		var wabiSabiConfig = new WabiSabiConfig();
		Assert.True(SigningTimeProvider.Trezor.GetSigningTime(300, 500) < wabiSabiConfig.MaximumSigningDelay);
	}
}

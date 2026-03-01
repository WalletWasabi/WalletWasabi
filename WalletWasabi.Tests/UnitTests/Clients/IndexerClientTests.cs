using Xunit;

namespace WalletWasabi.Tests.UnitTests.Clients;

public class IndexerClientTests
{
	[Fact]
	public void ConstantsTests()
	{
		var supported = int.Parse(WalletWasabi.Helpers.Constants.ClientSupportBackendVersion);
		var current = int.Parse(WalletWasabi.Helpers.Constants.BackendMajorVersion);

		Assert.True(supported == current);
	}
}

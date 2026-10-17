using WalletWasabi.Client.Rpc;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Rpc;

/// <summary>
/// Tests for <see cref="WasabiJsonRpcService"/>
/// </summary>
public class WasabiJsonRpcServiceTests
{
	[Fact]
	public void RenameWallet()
	{
		var service = new WasabiJsonRpcService(null!);

		// A valid wallet name is expected.
		Assert.Throws<ArgumentException>(() => service.RenameWallet(" "));
	}
}

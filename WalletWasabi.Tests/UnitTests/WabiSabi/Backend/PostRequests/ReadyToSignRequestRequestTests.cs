using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

/// <summary>
/// Tests for <see cref="ReadyToSignRequestRequest"/> class.
/// </summary>
public class ReadyToSignRequestRequestTests
{
	[Fact]
	public void EqualityTest()
	{
		uint256 roundId = BitcoinFactory.CreateUint256();
		Guid guid = Guid.NewGuid();

		// Request #1.
		ReadyToSignRequestRequest request1 = new(RoundId: roundId, AliceId: guid);

		// Request #2.
		ReadyToSignRequestRequest request2 = new(RoundId: roundId, AliceId: guid);

		Assert.Equal(request1, request2);

		// Request #3.
		ReadyToSignRequestRequest request3 = new(RoundId: BitcoinFactory.CreateUint256(), AliceId: guid);

		Assert.NotEqual(request1, request3);
	}
}

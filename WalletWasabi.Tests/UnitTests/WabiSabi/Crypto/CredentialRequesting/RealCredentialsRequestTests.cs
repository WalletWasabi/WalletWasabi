using NBitcoin.Secp256k1;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Crypto.CredentialRequesting;

/// <summary>
/// Tests for <see cref="RealCredentialsRequest"/> class.
/// </summary>
public class RealCredentialsRequestTests
{
	[Fact]
	public void EqualityTest()
	{
		// Request #1.
		RealCredentialsRequest request1 = NewRealCredentialsRequest(1);

		// Request #2.
		RealCredentialsRequest request2 = NewRealCredentialsRequest(1);

		Assert.Equal(request1, request2);

		// Request #3.
		RealCredentialsRequest request3 = NewRealCredentialsRequest(2);
		Assert.NotEqual(request1, request3);
	}

	private static GroupElement NewGroupElement(int i) => Generators.FromText($"T{i}");

	/// <param name="modifier">Modifier so that we generate a different request if we want.</param>
	private static RealCredentialsRequest NewRealCredentialsRequest(int modifier) =>
		new(
			123456L,
			new[] { new CredentialPresentation(NewGroupElement(7), NewGroupElement(7), NewGroupElement(7), NewGroupElement(7), NewGroupElement(7)) },
			new[] { new IssuanceRequest(ma: NewGroupElement(modifier * 1), bitCommitments: new GroupElement[] { NewGroupElement(2), NewGroupElement(3) }) },
			new[] { new Proof(new GroupElementVector(NewGroupElement(13)), new ScalarVector(new Scalar(5))) });
}

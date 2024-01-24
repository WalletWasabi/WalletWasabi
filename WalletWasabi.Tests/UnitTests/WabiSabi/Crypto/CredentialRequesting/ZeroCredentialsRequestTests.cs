using NBitcoin.Secp256k1;
using System.Linq;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Crypto.CredentialRequesting;

/// <summary>
/// Tests for <see cref="ZeroCredentialsRequest"/> class.
/// </summary>
public class ZeroCredentialsRequestTests
{
	[Fact]
	public void EqualityTest()
	{
		// Request #1.
		ZeroCredentialsRequest request1 = GetZeroCredentialsRequest(1);

		// Request #2.
		ZeroCredentialsRequest request2 = GetZeroCredentialsRequest(1);

		Assert.Equal(request1, request2);

		// Request #3.
		ZeroCredentialsRequest request3 = GetZeroCredentialsRequest(2);
		Assert.NotEqual(request1, request3);
	}

	private static GroupElement NewGroupElement(int i) => Generators.FromText($"T{i}");

	private static GroupElementVector NewGroupElementVector(params int[] arr) => new(arr.Select(i => NewGroupElement(i)));

	private static ScalarVector NewScalarVector(params uint[] arr) => new(arr.Select(i => new Scalar(i)));

	/// <remarks>Each instance represents the same request but a new object instance.</remarks>
	private static ZeroCredentialsRequest GetZeroCredentialsRequest(int modifier = 1)
	{
		IssuanceRequest[] requested = new IssuanceRequest[]
		{
				new IssuanceRequest(ma: NewGroupElement(modifier * 1), bitCommitments: new GroupElement[] { NewGroupElement(2), NewGroupElement(3) })
		};

		return new(requested,
					   new Proof[] { new Proof(publicNonces: NewGroupElementVector(1, 2), responses: NewScalarVector(6, 7)) });
	}
}

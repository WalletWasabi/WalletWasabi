using NBitcoin;
using NBitcoin.Secp256k1;
using System.Linq;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

/// <summary>
/// Tests for <see cref="ConnectionConfirmationRequest"/> class.
/// </summary>
public class ConnectionConfirmationRequestTests
{
	/// <summary>Implementation of equality rules is important for idempotent request cache.</summary>
	[Fact]
	public void EqualityTest()
	{
		uint256 roundId = BitcoinFactory.CreateUint256();
		Guid guid = Guid.NewGuid();

		// Request #1.
		ConnectionConfirmationRequest request1 = new(
			RoundId: roundId,
			AliceId: guid,
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1),
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 2),
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		// Request #2.
		ConnectionConfirmationRequest request2 = new(
			RoundId: roundId,
			AliceId: guid,
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1),
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 2),
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.Equal(request1, request2);

		// Request #3.
		ConnectionConfirmationRequest request3 = new(
			RoundId: BitcoinFactory.CreateUint256(), // Intentionally changed.
			AliceId: guid,
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1),
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 2),
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request3);

		// Request #4.
		ConnectionConfirmationRequest request4 = new(
			RoundId: roundId,
			AliceId: Guid.NewGuid(), // Intentionally changed.
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1),
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 2),
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request4);

		// Request #5.
		ConnectionConfirmationRequest request5 = new(
			RoundId: roundId,
			AliceId: guid,
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1337), // Intentionally changed.
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 2),
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request5);

		// Request #6.
		ConnectionConfirmationRequest request6 = new(
			RoundId: roundId,
			AliceId: guid,
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1),
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1337), // Intentionally changed.
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 2),
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request6);

		// Request #7.
		ConnectionConfirmationRequest request7 = new(
			RoundId: roundId,
			AliceId: guid,
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1),
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 1337), // Intentionally changed.
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 2)
		);

		Assert.NotEqual(request1, request7);

		// Request #8.
		ConnectionConfirmationRequest request8 = new(
			RoundId: roundId,
			AliceId: guid,
			ZeroAmountCredentialRequests: NewZeroCredentialsRequest(modifier: 1),
			RealAmountCredentialRequests: NewRealCredentialsRequest(modifier: 1),
			ZeroVsizeCredentialRequests: NewZeroCredentialsRequest(modifier: 2),
			RealVsizeCredentialRequests: NewRealCredentialsRequest(modifier: 1337) // Intentionally changed.
		);

		Assert.NotEqual(request1, request8);
	}

	private static GroupElement NewGroupElement(int i) => Generators.FromText($"T{i}");

	private static GroupElementVector NewGroupElementVector(params int[] arr) => new(arr.Select(i => NewGroupElement(i)));

	private static ScalarVector NewScalarVector(params uint[] arr) => new(arr.Select(i => new Scalar(i)));

	/// <remarks>Each instance represents the same request but a new object instance.</remarks>
	private static ZeroCredentialsRequest NewZeroCredentialsRequest(int modifier = 1)
	{
		IssuanceRequest[] requested = new IssuanceRequest[] { NewIssuanceRequest(modifier) };
		Proof[] proofs = new Proof[]
		{
				new Proof(publicNonces: NewGroupElementVector(1, 2), responses: NewScalarVector(6, 7))
		};

		return new ZeroCredentialsRequest(requested, proofs);
	}

	private static IssuanceRequest NewIssuanceRequest(int modifier)
		=> new(ma: NewGroupElement(modifier * 1), bitCommitments: new GroupElement[] { NewGroupElement(2), NewGroupElement(3) });

	/// <summary>Dummy method to create a new real credentials request for testing purposes only.</summary>
	/// <param name="modifier">Modifier so that we generate a different request if we want.</param>
	private static RealCredentialsRequest NewRealCredentialsRequest(int modifier) =>
		new(
			123456L,
			new[] { new CredentialPresentation(NewGroupElement(7), NewGroupElement(7), NewGroupElement(7), NewGroupElement(7), NewGroupElement(7)) },
			new[] { NewIssuanceRequest(modifier: 81 * modifier) },
			new[] { new Proof(new GroupElementVector(NewGroupElement(13)), NewScalarVector(5)) });
}

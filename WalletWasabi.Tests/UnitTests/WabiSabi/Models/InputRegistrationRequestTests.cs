using NBitcoin;
using NBitcoin.Secp256k1;
using System.Linq;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

/// <summary>
/// Tests for <see cref="InputRegistrationRequest"/> class.
/// </summary>
public class InputRegistrationRequestTests
{
	[Fact]
	public void EqualityTest()
	{
		uint256 roundId = BitcoinFactory.CreateUint256();
		uint256 roundHash = BitcoinFactory.CreateUint256();
		OutPoint outPoint = BitcoinFactory.CreateOutPoint();

		using Key key = new();

		// Request #1.
		InputRegistrationRequest request1 = new(
			RoundId: roundId,
			Input: outPoint,
			OwnershipProof: CreateOwnershipProof(key, roundHash),
			ZeroAmountCredentialRequests: GetZeroCredentialsRequest(),
			ZeroVsizeCredentialRequests: GetZeroCredentialsRequest()
		);

		// Request #2.
		InputRegistrationRequest request2 = new(
			RoundId: roundId,
			Input: outPoint,
			OwnershipProof: CreateOwnershipProof(key, roundHash),
			ZeroAmountCredentialRequests: GetZeroCredentialsRequest(),
			ZeroVsizeCredentialRequests: GetZeroCredentialsRequest()
		);

		Assert.Equal(request1, request2);

		// Request #3.
		InputRegistrationRequest request3 = new(
			RoundId: BitcoinFactory.CreateUint256(), // Intentionally changed.
			Input: outPoint,
			OwnershipProof: CreateOwnershipProof(key, roundHash),
			ZeroAmountCredentialRequests: GetZeroCredentialsRequest(),
			ZeroVsizeCredentialRequests: GetZeroCredentialsRequest()
		);

		Assert.NotEqual(request1, request3);

		// Request #4.
		InputRegistrationRequest request4 = new(
			RoundId: roundId,
			Input: BitcoinFactory.CreateOutPoint(), // Intentionally changed.
			OwnershipProof: CreateOwnershipProof(key, roundHash),
			ZeroAmountCredentialRequests: GetZeroCredentialsRequest(),
			ZeroVsizeCredentialRequests: GetZeroCredentialsRequest()
		);

		Assert.NotEqual(request1, request4);

		// Request #5.
		InputRegistrationRequest request5 = new(
			RoundId: roundId,
			Input: outPoint,
			OwnershipProof: CreateOwnershipProof(key, roundHash: BitcoinFactory.CreateUint256()), // Intentionally changed.
			ZeroAmountCredentialRequests: GetZeroCredentialsRequest(),
			ZeroVsizeCredentialRequests: GetZeroCredentialsRequest()
		);

		Assert.NotEqual(request1, request5);

		// Request #6.
		InputRegistrationRequest request6 = new(
			RoundId: roundId,
			Input: outPoint,
			OwnershipProof: CreateOwnershipProof(key, roundHash),
			ZeroAmountCredentialRequests: GetZeroCredentialsRequest(modifier: 2), // Intentionally changed.
			ZeroVsizeCredentialRequests: GetZeroCredentialsRequest()
		);

		Assert.NotEqual(request1, request6);

		// Request #7.
		InputRegistrationRequest request7 = new(
			RoundId: roundId,
			Input: outPoint,
			OwnershipProof: CreateOwnershipProof(key, roundHash),
			ZeroAmountCredentialRequests: GetZeroCredentialsRequest(),
			ZeroVsizeCredentialRequests: GetZeroCredentialsRequest(modifier: 2) // Intentionally changed.
		);

		Assert.NotEqual(request1, request7);
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

	/// <remarks>Each instance represents the same proof but a new object instance.</remarks>
	public static OwnershipProof CreateOwnershipProof(Key key, uint256 roundHash)
		=> OwnershipProof.GenerateCoinJoinInputProof(
			key,
			new OwnershipIdentifier(Key.Parse("5KbdaBwc9Eit2LrmDp1WfZd815StNstwHanbRrPpGGN6wWJKyHe", Network.Main), key.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit)),
			WabiSabiFactory.CreateCommitmentData(roundHash),
			ScriptPubKeyType.Segwit);
}

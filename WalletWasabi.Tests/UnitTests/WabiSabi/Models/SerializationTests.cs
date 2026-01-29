using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using NBitcoin.Secp256k1;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Serialization;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

public class SerializationTests
{
	private static IEnumerable<GroupElement> Points = Enumerable.Range(0, int.MaxValue).Select(i => Generators.FromText($"T{i}"));
	private static IEnumerable<Scalar> Scalars = Enumerable.Range(1, int.MaxValue).Select(i => new Scalar((uint)i));
	private static CredentialIssuerSecretKey IssuerKey = new(InsecureRandom.Instance);

	[Fact]
	public void InputRegistrationRequestMessageSerialization()
	{
		var message = new InputRegistrationRequest(
			BitcoinFactory.CreateUint256(),
			BitcoinFactory.CreateOutPoint(),
			new OwnershipProof(),
			CreateZeroCredentialsRequest(),
			CreateZeroCredentialsRequest());

		AssertSerialization(message);
	}

	[Fact]
	public void InputRegistrationResponseMessageSerialization()
	{
		var message = new InputRegistrationResponse(
			Guid.NewGuid(),
			CreateCredentialsResponse(),
			CreateCredentialsResponse());

		AssertSerialization(message);
	}

	[Fact]
	public void ConnectionConfirmationRequestMessageSerialization()
	{
		var message = new ConnectionConfirmationRequest(
			BitcoinFactory.CreateUint256(),
			Guid.NewGuid(),
			CreateZeroCredentialsRequest(),
			CreateRealCredentialsRequest(),
			CreateZeroCredentialsRequest(),
			CreateRealCredentialsRequest());

		AssertSerialization(message);
	}

	[Fact]
	public void ConnectionConfirmationResponseMessageSerialization()
	{
		var message = new ConnectionConfirmationResponse(
			CreateCredentialsResponse(),
			CreateCredentialsResponse(),
			CreateCredentialsResponse(),
			CreateCredentialsResponse());

		AssertSerialization(message);
	}

	[Fact]
	public void OutputRegistrationRequestMessageSerialization()
	{
		var message = new OutputRegistrationRequest(
			BitcoinFactory.CreateUint256(),
			BitcoinFactory.CreateScript(),
			CreateRealCredentialsRequest(),
			CreateRealCredentialsRequest());

		AssertSerialization(message);
	}

	[Fact]
	public void ReissueCredentialRequestMessageSerialization()
	{
		var message = new ReissueCredentialRequest(
			BitcoinFactory.CreateUint256(),
			CreateRealCredentialsRequest(),
			CreateRealCredentialsRequest(),
			CreateZeroCredentialsRequest(),
			CreateZeroCredentialsRequest());

		AssertSerialization(message);
	}

	[Fact]
	public void ReissueCredentialResponseMessageSerialization()
	{
		var message = new ReissueCredentialResponse(
			CreateCredentialsResponse(),
			CreateCredentialsResponse(),
			CreateCredentialsResponse(),
			CreateCredentialsResponse());

		AssertSerialization(message);
	}

	[Fact]
	public void InpuRemovalRequestMessageSerialization()
	{
		var message = new InputsRemovalRequest(
			BitcoinFactory.CreateUint256(),
			Guid.NewGuid());

		AssertSerialization(message);
	}

	[Fact]
	public void TransactionSignatureRequestMessageSerialization()
	{
		using var key1 = new Key();
		using var key2 = new Key();
		var message = new TransactionSignaturesRequest(BitcoinFactory.CreateUint256(), 1, new WitScript(Op.GetPushOp(key1.PubKey.ToBytes())));

		AssertSerialization(message);
	}

	[Fact]
	public void RoundStateRequestSerialization()
	{
		RoundStateCheckpoint stateCheckpoint = new(uint256.One, 0);
		RoundStateRequest request = new(ImmutableList.Create(stateCheckpoint));

		AssertSerialization(request);
	}

	[Fact]
	public void RoundStateResponseSerialization()
	{
		var round = WabiSabiFactory.CreateRound(new WabiSabiConfig());
		var roundState = RoundState.FromRound(round);
		RoundStateResponse response = new([roundState]);

		AssertSerialization(response);

		var state = round.CoinjoinState;
		(var coin, var ownershipProof) = WabiSabiFactory.CreateCoinWithOwnershipProof(roundId: round.Id);
		state = state.AddInput(coin, ownershipProof, WabiSabiFactory.CreateCommitmentData(round.Id));
		round.CoinjoinState = new MultipartyTransactionState(state.Parameters, state.Events);
		roundState = RoundState.FromRound(round);

		response = new([roundState]);

		AssertSerialization(response);
	}

	private static void AssertSerialization<T>(T message)
	{
		var serializedMessage = JsonEncoder.ToString(message, Encode.CoordinatorMessage);
		var deserializedMessage = Decode.CoordinatorMessage<T>(serializedMessage);
		var reserializedMessage = JsonEncoder.ToString(deserializedMessage, Encode.CoordinatorMessage);
		Assert.Equal(reserializedMessage, serializedMessage);
	}

	private static ZeroCredentialsRequest CreateZeroCredentialsRequest() =>
		new(
			new[] { new IssuanceRequest(Points.First(), Points.Take(2)) },
			new[] { new Proof(new GroupElementVector(Points.Take(2)), new ScalarVector(Scalars.Take(2))) });

	private static RealCredentialsRequest CreateRealCredentialsRequest() =>
		new(
			123456L,
			new[] { new CredentialPresentation(Points.First(), Points.First(), Points.First(), Points.First(), Points.First()) },
			new[] { new IssuanceRequest(Points.First(), Points.Take(2)) },
			new[] { new Proof(new GroupElementVector(Points.Take(2)), new ScalarVector(Scalars.Take(2))) });

	private static CredentialsResponse CreateCredentialsResponse() =>
		new(
			new[] { MAC.ComputeMAC(IssuerKey, Points.First(), Scalars.First()) },
			new[] { new Proof(new GroupElementVector(Points.Take(2)), new ScalarVector(Scalars.Take(2))) });
}

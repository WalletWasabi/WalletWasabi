using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using NBitcoin.Secp256k1;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Coordinator.Controllers;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Serialization;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi;

public class SerializationTests
{
	[Fact]
	public void OversizedCredentialRequestIsRejected()
	{
		const string pt = "0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798";
		const string scalar = "0000000000000000000000000000000000000000000000000000000000000001";

		string presentation = $"{{\"Ca\":\"{pt}\",\"Cx0\":\"{pt}\",\"Cx1\":\"{pt}\",\"CV\":\"{pt}\",\"S\":\"{pt}\"}}";

		string RequestWithNonces(int n) =>
			"{\"Delta\":0,\"Presented\":[],\"Requested\":[],\"Proofs\":[{\"PublicNonces\":["
			+ string.Join(",", Enumerable.Repeat($"\"{pt}\"", n))
			+ $"],\"Responses\":[\"{scalar}\"]}}]}}";

		string RequestWithPresentations(int n) =>
			"{\"Delta\":0,\"Presented\":["
			+ string.Join(",", Enumerable.Repeat(presentation, n))
			+ "],\"Requested\":[],\"Proofs\":[]}";

		// The largest legitimate proof (a full-supply range proof has 2w+1 public nonces) decodes.
		Assert.NotNull(JsonDecoder.FromString(RequestWithNonces(WalletWasabi.WabiSabi.ProtocolConstants.MaxProofNonces), Decode.RealCredentialsRequest));
		Assert.NotNull(JsonDecoder.FromString(RequestWithPresentations(WalletWasabi.WabiSabi.ProtocolConstants.CredentialNumber), Decode.RealCredentialsRequest));

		// One element past the bound is rejected before the whole collection is materialized.
		Assert.Null(JsonDecoder.FromString(RequestWithNonces(WalletWasabi.WabiSabi.ProtocolConstants.MaxProofNonces + 1), Decode.RealCredentialsRequest));
		Assert.Null(JsonDecoder.FromString(RequestWithPresentations(WalletWasabi.WabiSabi.ProtocolConstants.CredentialNumber + 1), Decode.RealCredentialsRequest));

		// The oversized collection that enabled unauthenticated CPU exhaustion is rejected outright.
		Assert.Null(JsonDecoder.FromString(RequestWithNonces(100_000), Decode.RealCredentialsRequest));
	}

	[Fact]
	public void OversizedRoundStateRequestIsRejected()
	{
		const string checkpoint = "{\"RoundId\":\"0000000000000000000000000000000000000000000000000000000000000000\",\"StateId\":0}";

		string RequestWithCheckpoints(int n) =>
			"{\"RoundCheckpoints\":["
			+ string.Join(",", Enumerable.Repeat(checkpoint, n))
			+ "]}";

		// A status request with the maximum number of tracked rounds decodes.
		Assert.NotNull((object?)Decode.CoordinatorMessage<RoundStateRequest>(RequestWithCheckpoints(WalletWasabi.WabiSabi.ProtocolConstants.MaxRoundCheckpoints)));

		// One element past the bound is rejected before the whole collection is materialized.
		Assert.Null((object?)Decode.CoordinatorMessage<RoundStateRequest>(RequestWithCheckpoints(WalletWasabi.WabiSabi.ProtocolConstants.MaxRoundCheckpoints + 1)));

		// The oversized unauthenticated checkpoint array is rejected outright.
		Assert.Null((object?)Decode.CoordinatorMessage<RoundStateRequest>(RequestWithCheckpoints(100_000)));
	}

	[Fact]
	public void RequestSizeCapCoversTheLargestValidRequestAndBoundsThePreAuthParse()
	{
		// A group element serializes as 33 bytes of hex and a scalar as 32, quoted.
		const int groupElement = 68;
		const int scalar = 66;

		int proof = WalletWasabi.WabiSabi.ProtocolConstants.MaxProofNonces * groupElement
			+ WalletWasabi.WabiSabi.ProtocolConstants.MaxProofResponses * scalar;
		int issuance = groupElement + WalletWasabi.WabiSabi.ProtocolConstants.MaxRangeProofWidth * groupElement;
		int credentialRequest =
			WalletWasabi.WabiSabi.ProtocolConstants.CredentialNumber * (5 * groupElement)
			+ WalletWasabi.WabiSabi.ProtocolConstants.CredentialNumber * issuance
			+ WalletWasabi.WabiSabi.ProtocolConstants.MaxProofsPerRequest * proof;

		// The largest message carries four credential requests.
		int largestValidRequest = 4 * credentialRequest;

		// The cap never rejects a valid request and stays far below the Kestrel default (~30 MB).
		Assert.True(WalletWasabi.WabiSabi.ProtocolConstants.MaxRequestSize > largestValidRequest);
		Assert.True(WalletWasabi.WabiSabi.ProtocolConstants.MaxRequestSize < 30_000_000);

		// The limit is wired onto the coordinator endpoints.
		Assert.NotNull(typeof(WabiSabiController).GetCustomAttribute<RequestSizeLimitAttribute>());
	}

	[Fact]
	public void IssuanceRequestSerialization()
	{
		// Serialization round test.
		var issuanceRequest = new IssuanceRequest(Generators.G, Enumerable.Range(0, 5).Select(i => Generators.FromText($"G{i}")));
		var serializedIssuanceRequest = JsonEncoder.ToString(issuanceRequest, Encode.IssuanceRequest);

		var deserializedIssuanceRequest = JsonDecoder.FromString(serializedIssuanceRequest, Decode.IssuanceRequest)!;
		Assert.Equal(issuanceRequest.Ma, deserializedIssuanceRequest.Ma);
		Assert.Equal(issuanceRequest.BitCommitments, deserializedIssuanceRequest.BitCommitments);

		// Compatibility test (can be remove in the near future
		string serializedWithPreviousVersion = "{\"Ma\":\"0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798\",\"BitCommitments\":[\"02A4E32FE4402666F187BA946FCC166ECBCB5C16BDDF1FF05806AF75FC36678244\",\"027B778E4C1D1F33C90C619ED9BDA321BBC5F05CF9F131A326C57FDA87359D3B0B\",\"02B5EF029E52D2996188804B0BCD2242E1D0F042005A4CC5BF1A364BDF346B6434\",\"030DF036B638077D4B8612C0F7454EF59E8F957D8C149B87FA412B0A80A3AC0B89\",\"020D2A12B37E41DD4E8F2D36862D24FEE4C06586A94296E2543ABBBD1A2ABA6E90\"]}";
		Assert.Equal(serializedWithPreviousVersion, serializedIssuanceRequest);
	}

	[Fact]
	public void CredentialResponseSerialization()
	{
		var rnd = new InsecureRandom(1234);
		var points = Enumerable.Range(0, int.MaxValue).Select(i => Generators.FromText($"T{i}"));
		var scalars = Enumerable.Range(1, int.MaxValue).Select(i => new Scalar((uint)i));
		var issuerKey = new CredentialIssuerSecretKey(rnd);

		var credentialRespose =
			new CredentialsResponse(
				new[] { MAC.ComputeMAC(issuerKey, points.First(), scalars.First()) },
				new[] { new Proof(new GroupElementVector(points.Take(2)), new ScalarVector(scalars.Take(2))) });

		var serializedCredentialsResponse = JsonEncoder.ToString(credentialRespose, Encode.CredentialsResponse);

		var deserializedCredentialsResponse = JsonDecoder.FromString(serializedCredentialsResponse, Decode.CredentialsResponse)!;
		Assert.Equal(credentialRespose.IssuedCredentials, deserializedCredentialsResponse.IssuedCredentials);
		Assert.Equal(credentialRespose.Proofs, deserializedCredentialsResponse.Proofs);

		string serializedAsPreviousVersion =
			"{\"issuedCredentials\":[{\"T\":\"0000000000000000000000000000000000000000000000000000000000000001\",\"V\":\"02CE327D741569B1296A6C8BCD428508E86E365881F940BEE86EDC30B7C1C7A6F6\"}],\"proofs\":[{\"PublicNonces\":[\"03330CFC201A4AC78F9A0D2BC5FD78E22882D5769AE9939502F32A6408FDD08FC7\",\"021F93603DB53BFAD5C92390F735D0CBB8617B4AB8214AE91C5664A3D1E9B009C8\"],\"Responses\":[\"0000000000000000000000000000000000000000000000000000000000000001\",\"0000000000000000000000000000000000000000000000000000000000000002\"]}]}";
		Assert.Equal(serializedAsPreviousVersion, serializedCredentialsResponse);
	}

	[Fact]
	public void RegistrationMessageSerialization()
	{
		SecureRandom rnd = SecureRandom.Instance;
		var sk = new CredentialIssuerSecretKey(rnd);

		var issuer = new CredentialIssuer(sk, rnd, 4_300_000_000_000);
		var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4_300_000_000_000);
		(ZeroCredentialsRequest zeroCredentialsRequest, CredentialsResponseValidation validationData) = client.CreateRequestForZeroAmount();
		var credentialResponse = issuer.HandleRequest(zeroCredentialsRequest);
		var present = client.HandleResponse(credentialResponse, validationData);
		(RealCredentialsRequest realCredentialsRequest, _) = client.CreateRequest(new[] { 1L }, present, CancellationToken.None);

		// Registration request message.
		var serializedRequestMessage = JsonEncoder.ToString(realCredentialsRequest, Encode.RealCredentialsRequest);
		var deserializedCredentialsRequest = JsonDecoder.FromString(serializedRequestMessage, Decode.RealCredentialsRequest);

		Assert.NotSame(zeroCredentialsRequest, deserializedCredentialsRequest);

		var deserializedRequestMessage = JsonDecoder.FromString(serializedRequestMessage, Decode.RealCredentialsRequest);
		var reserializedRequestMessage = JsonEncoder.ToString(deserializedRequestMessage!, Encode.RealCredentialsRequest);
		Assert.Equal(serializedRequestMessage, reserializedRequestMessage);

		// Registration response message.
		var serializedResponseMessage = JsonEncoder.ToString(credentialResponse, Encode.CredentialsResponse);
		var deserializedResponseMessage = JsonDecoder.FromString(serializedResponseMessage, Decode.CredentialsResponse);
		var reserializedResponseMessage = JsonEncoder.ToString(deserializedResponseMessage!, Encode.CredentialsResponse);

		Assert.Equal(serializedResponseMessage, reserializedResponseMessage);
	}


	[Fact]
	public void BtcPayServerCoordinatorCompatibility()
	{
		var jsonString = """{"RoundStates":[{"Id":"c9fe80f588809feabf11043bf3e5952e9a6e861b68f3ca451b34507243545bd4","BlameOf":"0000000000000000000000000000000000000000000000000000000000000000","AmountCredentialIssuerParameters":{"Cw":"0354CB0724F653CC56C94C13ED949DE33378D3B88086E39932D42E27535B063AE6","I":"036018B97A215A680837671734CDC4CD8355AA56C45CA48E71CE7A6B51ACC856D6"},"VsizeCredentialIssuerParameters":{"Cw":"0332E8D9DD6116F49991044A97EF59E9AB081628B1E03ABA9BA3C71FCAF8354D40","I":"0358F51C185A8931BC5DAA1C85B5D63F0FD0C0481EE2EED8E92D4F4585A3F8307B"},"Phase":0,"EndRoundState":0,"InputRegistrationStart":"2025-04-22T14:10:12.970806+00:00","InputRegistrationTimeout":"0d 1h 0m 0s","CoinjoinState":{"Type":"ConstructionState","Events":[{"Type":"RoundCreated","RoundParameters":{"Network":"Main","MiningFeeRate":2000,"CoordinationFeeRate":{"Rate":0.0,"PlebsDontPayThreshold":0},"MaxSuggestedAmount":4300000000000,"MinInputCountByRound":21,"MaxInputCountByRound":210,"AllowedInputAmounts":{"Min":5000,"Max":4300000000000},"AllowedOutputAmounts":{"Min":5000,"Max":4300000000000},"AllowedInputTypes":[4,7],"AllowedOutputTypes":[0,1,2,3,4,5,6,7],"StandardInputRegistrationTimeout":"0d 1h 0m 0s","ConnectionConfirmationTimeout":"0d 0h 1m 0s","OutputRegistrationTimeout":"0d 0h 2m 10s","TransactionSigningTimeout":"0d 0h 2m 10s","BlameInputRegistrationTimeout":"0d 0h 3m 30s","MinAmountCredentialValue":5000,"MaxAmountCredentialValue":4300000000000,"InitialInputVsizeAllocation":99985,"MaxVsizeCredentialValue":255,"MaxVsizeAllocationPerAlice":255,"CoordinationIdentifier":"CoinJoinCoordinatorIdentifier","DelayTransactionSigning":false,"MaxTransactionSize":100000,"MinRelayTxFee":1000}}]},"InputRegistrationEnd":"2025-04-22T15:10:12.970806+00:00","IsBlame":false}],"CoinJoinFeeRateMedians":[{"TimeFrame":"1d 0h 0m 0s","MedianFeeRate":2000},{"TimeFrame":"7d 0h 0m 0s","MedianFeeRate":2000},{"TimeFrame":"30d 0h 0m 0s","MedianFeeRate":2000}]}""";
		var status = Decode.CoordinatorMessage<RoundStateResponse>(jsonString);
		Assert.NotNull(status);
	}
}

using System.Linq;
using System.Threading;
using NBitcoin.Secp256k1;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi;

public class SerializationTests
{
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
}

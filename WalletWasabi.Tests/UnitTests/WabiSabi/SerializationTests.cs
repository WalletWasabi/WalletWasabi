using System.Linq;
using System.Threading;
using NBitcoin;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WabiSabi;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Groups;
using WabiSabi.Crypto.ZeroKnowledge;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.WabiSabi.Crypto.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi;

public class SerializationTests
{
	[Fact]
	public void GroupElementSerialization()
	{
		var converters = new JsonConverter[]
		{
				new GroupElementJsonConverter()
		};

		// Deserialization invalid elements.
		var ex = Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<GroupElement>("21", converters));
		Assert.StartsWith("No valid serialized GroupElement", ex.Message);

		ex = Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<GroupElement>("\"\"", converters));
		Assert.StartsWith("Parameter must be 33. Actual: 0.", ex.Message);

		ex = Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<GroupElement>("\"0000000000000000000000000000000000000000000000000000000000000000\"", converters));
		Assert.StartsWith("Parameter must be 33. Actual: 32.", ex.Message);

		// Serialization Infinity test.
		var serializedInfinityGroupElement = JsonConvert.SerializeObject(GroupElement.Infinity, converters);
		Assert.Equal("\"000000000000000000000000000000000000000000000000000000000000000000\"", serializedInfinityGroupElement);

		var deserializedInfinityGroupElement = JsonConvert.DeserializeObject<GroupElement>("\"000000000000000000000000000000000000000000000000000000000000000000\"", converters);
		Assert.Equal(GroupElement.Infinity, deserializedInfinityGroupElement);

		// Serialization round test.
		var serializedGroupElement = JsonConvert.SerializeObject(Generators.G, converters);
		Assert.Equal("\"0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798\"", serializedGroupElement);

		var deserializedGroupElement = JsonConvert.DeserializeObject<GroupElement>("\"0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798\"", converters);
		Assert.Equal(Generators.G, deserializedGroupElement);
	}

	[Fact]
	public void GroupElementCollectionSerialization()
	{
		var converters = new JsonConverter[]
		{
			new GroupElementJsonConverter(),
			new GroupElementVectorJsonConverter()
		};

		// Serialization collection test.
		var serializedGroupElements = JsonConvert.SerializeObject(new[] { Generators.Gx0, Generators.Gx1 }, converters);
		Assert.Equal("[\"02E33C9F3CBE6388A2D3C3ECB12153DB73499928541905D86AAA4FFC01F2763B54\",\"0246253CC926AAB789BAA278AB9A54EDEF455CA2014038E9F84DE312C05A8121CC\"]", serializedGroupElements);

		var deserializedGroupElements = JsonConvert.DeserializeObject<GroupElement[]>("[\"02E33C9F3CBE6388A2D3C3ECB12153DB73499928541905D86AAA4FFC01F2763B54\",\"0246253CC926AAB789BAA278AB9A54EDEF455CA2014038E9F84DE312C05A8121CC\"]", converters)!;
		Assert.Equal(Generators.Gx0, deserializedGroupElements[0]);
		Assert.Equal(Generators.Gx1, deserializedGroupElements[1]);

		var deserializedGroupElementVector = JsonConvert.DeserializeObject<GroupElementVector>("[\"02E33C9F3CBE6388A2D3C3ECB12153DB73499928541905D86AAA4FFC01F2763B54\",\"0246253CC926AAB789BAA278AB9A54EDEF455CA2014038E9F84DE312C05A8121CC\"]", converters);
		Assert.NotNull(deserializedGroupElementVector);
		Assert.Equal(deserializedGroupElements, deserializedGroupElementVector);
	}

	[Fact]
	public void IssuanceRequestSerialization()
	{
		var converters = new JsonConverter[]
		{
			new GroupElementJsonConverter(),
			new IssuanceRequestJsonConverter()
		};

		// Serialization round test.
		var issuanceRequest = new IssuanceRequest(Generators.G, Enumerable.Range(0, 5).Select(i => Generators.FromText($"G{i}")));
		var serializedIssuanceRequest = JsonConvert.SerializeObject(issuanceRequest, converters);

		var deserializedIssuanceRequest = JsonConvert.DeserializeObject<IssuanceRequest>(serializedIssuanceRequest, converters)!;
		Assert.Equal(issuanceRequest.Ma, deserializedIssuanceRequest.Ma);
		Assert.Equal(issuanceRequest.BitCommitments, deserializedIssuanceRequest.BitCommitments);

		// Compatibility test (can be remove in the near future
		string serializedWithPreviousVersion = "{\"Ma\":\"0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798\",\"BitCommitments\":[\"02A4E32FE4402666F187BA946FCC166ECBCB5C16BDDF1FF05806AF75FC36678244\",\"027B778E4C1D1F33C90C619ED9BDA321BBC5F05CF9F131A326C57FDA87359D3B0B\",\"02B5EF029E52D2996188804B0BCD2242E1D0F042005A4CC5BF1A364BDF346B6434\",\"030DF036B638077D4B8612C0F7454EF59E8F957D8C149B87FA412B0A80A3AC0B89\",\"020D2A12B37E41DD4E8F2D36862D24FEE4C06586A94296E2543ABBBD1A2ABA6E90\"]}";
		Assert.Equal(serializedWithPreviousVersion, serializedIssuanceRequest);
	}

	[Fact]
	public void CredentialResponseSerialization()
	{
		var converters = new JsonConverter[]
		{
			new ScalarJsonConverter(),
			new ScalarVectorJsonConverter(),
			new GroupElementJsonConverter(),
			new GroupElementVectorJsonConverter(),
			new MacJsonConverter(),
			new ProofJsonConverter()
		};

		var rnd = new InsecureRandom(1234);
		var points = Enumerable.Range(0, int.MaxValue).Select(i => Generators.FromText($"T{i}"));
		var scalars = Enumerable.Range(1, int.MaxValue).Select(i => new Scalar((uint)i));
		var issuerKey = new CredentialIssuerSecretKey(rnd);

		var credentialRespose =
			new CredentialsResponse(
				new[] { MAC.ComputeMAC(issuerKey, points.First(), scalars.First()) },
				new[] { new Proof(new GroupElementVector(points.Take(2)), new ScalarVector(scalars.Take(2))) });

		var serializedCredentialsResponse = JsonConvert.SerializeObject(credentialRespose, converters);

		var deserializedCredentialsResponse = JsonConvert.DeserializeObject<CredentialsResponse>(serializedCredentialsResponse, converters)!;
		Assert.Equal(credentialRespose.IssuedCredentials, deserializedCredentialsResponse.IssuedCredentials);
		Assert.Equal(credentialRespose.Proofs, deserializedCredentialsResponse.Proofs);

		string serializedAsPreviousVersion =
			"{\"IssuedCredentials\":[{\"T\":\"0000000000000000000000000000000000000000000000000000000000000001\",\"V\":\"02CE327D741569B1296A6C8BCD428508E86E365881F940BEE86EDC30B7C1C7A6F6\"}],\"Proofs\":[{\"PublicNonces\":[\"03330CFC201A4AC78F9A0D2BC5FD78E22882D5769AE9939502F32A6408FDD08FC7\",\"021F93603DB53BFAD5C92390F735D0CBB8617B4AB8214AE91C5664A3D1E9B009C8\"],\"Responses\":[\"0000000000000000000000000000000000000000000000000000000000000001\",\"0000000000000000000000000000000000000000000000000000000000000002\"]}]}";
		Assert.Equal(serializedAsPreviousVersion, serializedCredentialsResponse);
	}

	[Fact]
	public void ScalarSerialization()
	{
		var converters = new JsonConverter[]
		{
				new ScalarJsonConverter()
		};

		// Deserialization invalid scalar.
		var ex = Assert.Throws<ArgumentException>(() => JsonConvert.DeserializeObject<Scalar>("21", converters));
		Assert.StartsWith("No valid serialized Scalar", ex.Message);

		Assert.Throws<IndexOutOfRangeException>(() => JsonConvert.DeserializeObject<Scalar>("\"\"", converters));

		// Serialization Zero test.
		var serializedZero = JsonConvert.SerializeObject(Scalar.Zero, converters);
		Assert.Equal("\"0000000000000000000000000000000000000000000000000000000000000000\"", serializedZero);

		var deserializedZero = JsonConvert.DeserializeObject<Scalar>("\"0000000000000000000000000000000000000000000000000000000000000000\"", converters);
		Assert.Equal(Scalar.Zero, deserializedZero);

		// Serialization round test.
		var scalar = new Scalar(ByteHelpers.FromHex("D9C17A80D299A51E1ED9CF94FCE5FD883ADACE4ECC167E1D1FB8E5C4A0ADC4D2"));

		var serializedScalar = JsonConvert.SerializeObject(scalar, converters);
		Assert.Equal("\"D9C17A80D299A51E1ED9CF94FCE5FD883ADACE4ECC167E1D1FB8E5C4A0ADC4D2\"", serializedScalar);

		var deserializedScalar = JsonConvert.DeserializeObject<Scalar>("\"D9C17A80D299A51E1ED9CF94FCE5FD883ADACE4ECC167E1D1FB8E5C4A0ADC4D2\"", converters);
		Assert.Equal(scalar, deserializedScalar);
	}

	[Fact]
	public void ScalarCollectionSerialization()
	{
		var converters = new JsonConverter[]
		{
			new ScalarJsonConverter(),
			new ScalarVectorJsonConverter()
		};

		// Serialization collection test.
		var one = Scalar.One;
		var two = one + one;
		var three = two + one;
		var other = two.Sqr(8) + three;

		var serializedScalars = JsonConvert.SerializeObject(new[] { one, two }, converters);
		Assert.Equal("[\"0000000000000000000000000000000000000000000000000000000000000001\",\"0000000000000000000000000000000000000000000000000000000000000002\"]", serializedScalars);

		var deserializedScalars = JsonConvert.DeserializeObject<Scalar[]>("[\"000000000000000000000000000000014551231950B75FC4402DA1732FC9BEC2\",\"0000000000000000000000000000000000000000000000000000000000000003\"]", converters)!;
		Assert.Equal(other, deserializedScalars[0]);
		Assert.Equal(three, deserializedScalars[1]);

		var deserializedScalarVector = JsonConvert.DeserializeObject<ScalarVector>("[\"000000000000000000000000000000014551231950B75FC4402DA1732FC9BEC2\",\"0000000000000000000000000000000000000000000000000000000000000003\"]", converters);
		Assert.NotNull(deserializedScalarVector);
		Assert.Equal(deserializedScalars, deserializedScalarVector);
	}

	[Fact]
	public void MoneySerialization()
	{
		JsonConverter[] converters = new JsonConverter[]
		{
				new MoneySatoshiJsonConverter()
		};

		// Simple values.
		Assert.Equal(Money.Satoshis(0), JsonConvert.DeserializeObject<Money>("0", converters));
		Assert.Equal(Money.Satoshis(1), JsonConvert.DeserializeObject<Money>("1", converters));
		Assert.Equal(Money.Satoshis(-1), JsonConvert.DeserializeObject<Money>("-1", converters));
		Assert.Equal(Money.Satoshis(16), JsonConvert.DeserializeObject<Money>("0x10", converters));
		Assert.Null(JsonConvert.DeserializeObject<Money>("", converters));

		// Simple round-trip test.
		Money money = Money.Satoshis(100);
		Assert.Equal("100", JsonConvert.SerializeObject(money, converters));
		Assert.Equal(money, JsonConvert.DeserializeObject<Money>("100", converters));

		// Out-of-range.
		Assert.Throws<InvalidCastException>(() => JsonConvert.DeserializeObject<Money>("90000000000000000000000000", converters));
		Assert.Throws<InvalidCastException>(() => JsonConvert.DeserializeObject<Money>("-90000000000000000000000000", converters));

		// Invalid values.
		Assert.Throws<ArgumentNullException>(() => JsonConvert.DeserializeObject<Money>(null!, converters));
		Assert.Throws<InvalidCastException>(() => JsonConvert.DeserializeObject<Money>("0.1", converters));
		Assert.Throws<InvalidCastException>(() => JsonConvert.DeserializeObject<Money>("1e6", converters));
		Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<Money>("Satoshi", converters));
	}

	[Fact]
	public void RegistrationMessageSerialization()
	{
		var converters = new JsonConverter[]
		{
				new ScalarJsonConverter(),
				new ScalarVectorJsonConverter(),
				new GroupElementJsonConverter(),
				new GroupElementVectorJsonConverter(),
				new MoneySatoshiJsonConverter(),
				new CredentialPresentationJsonConverter(),
				new IssuanceRequestJsonConverter(),
				new ProofJsonConverter(),
				new MacJsonConverter()
		};

		SecureRandom rnd = SecureRandom.Instance;
		var sk = new CredentialIssuerSecretKey(rnd);

		var issuer = new CredentialIssuer(sk, rnd, 4_300_000_000_000);
		var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4_300_000_000_000);
		(ICredentialsRequest credentialRequest, CredentialsResponseValidation validationData) = client.CreateRequestForZeroAmount();
		var credentialResponse = issuer.HandleRequest(credentialRequest);
		var present = client.HandleResponse(credentialResponse, validationData);
		(credentialRequest, _) = client.CreateRequest(new[] { 1L }, present, CancellationToken.None);

		// Registration request message.
		var serializedRequestMessage = JsonConvert.SerializeObject(credentialRequest, converters);
		ZeroCredentialsRequest deserializedCredentialsRequest = JsonConvert.DeserializeObject<ZeroCredentialsRequest>(serializedRequestMessage, converters)!;
		Assert.NotSame(credentialRequest, deserializedCredentialsRequest);

		var deserializedRequestMessage = JsonConvert.DeserializeObject<RealCredentialsRequest>(serializedRequestMessage, converters);
		var reserializedRequestMessage = JsonConvert.SerializeObject(deserializedRequestMessage, converters);
		Assert.Equal(serializedRequestMessage, reserializedRequestMessage);

		// Registration response message.
		var serializedResponseMessage = JsonConvert.SerializeObject(credentialResponse, converters);
		var deserializedResponseMessage = JsonConvert.DeserializeObject<CredentialsResponse>(serializedResponseMessage, converters);
		var reserializedResponseMessage = JsonConvert.SerializeObject(deserializedResponseMessage, converters);
		Assert.Equal(serializedResponseMessage, reserializedResponseMessage);
	}
}

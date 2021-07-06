using System;
using System.Linq;
using System.Threading;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Crypto.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi
{
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
				new GroupElementJsonConverter()
			};

			// Serialization collection test.
			var serializedGroupElements = JsonConvert.SerializeObject(new[] { Generators.Gx0, Generators.Gx1 }, converters);
			Assert.Equal("[\"02E33C9F3CBE6388A2D3C3ECB12153DB73499928541905D86AAA4FFC01F2763B54\",\"0246253CC926AAB789BAA278AB9A54EDEF455CA2014038E9F84DE312C05A8121CC\"]", serializedGroupElements);

			var deserializedGroupElements = JsonConvert.DeserializeObject<GroupElement[]>("[\"02E33C9F3CBE6388A2D3C3ECB12153DB73499928541905D86AAA4FFC01F2763B54\",\"0246253CC926AAB789BAA278AB9A54EDEF455CA2014038E9F84DE312C05A8121CC\"]", converters);
			Assert.Equal(Generators.Gx0, deserializedGroupElements[0]);
			Assert.Equal(Generators.Gx1, deserializedGroupElements[1]);

			var deserializedGroupElementVector = JsonConvert.DeserializeObject<GroupElementVector>("[\"02E33C9F3CBE6388A2D3C3ECB12153DB73499928541905D86AAA4FFC01F2763B54\",\"0246253CC926AAB789BAA278AB9A54EDEF455CA2014038E9F84DE312C05A8121CC\"]", converters);
			Assert.Equal(deserializedGroupElements, deserializedGroupElementVector);
		}

		[Fact]
		public void IssuanceRequestSerialization()
		{
			var converters = new JsonConverter[]
			{
				new GroupElementJsonConverter()
			};

			// Serialization round test.
			var issuanceRequest = new IssuanceRequest(Generators.G, Enumerable.Range(0, 5).Select(i => Generators.FromText($"G{i}")));
			var serializedIssuanceRequest = JsonConvert.SerializeObject(issuanceRequest, converters);

			var deserializedIssuanceRequest = JsonConvert.DeserializeObject<IssuanceRequest>(serializedIssuanceRequest, converters);
			Assert.Equal(issuanceRequest.Ma, deserializedIssuanceRequest.Ma);
			Assert.Equal(issuanceRequest.BitCommitments, deserializedIssuanceRequest.BitCommitments);
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
				new ScalarJsonConverter()
			};

			// Serialization collection test.
			var one = Scalar.One;
			var two = one + one;
			var three = two + one;
			var other = two.Sqr(8) + three;

			var serializedScalars = JsonConvert.SerializeObject(new[] { one, two }, converters);
			Assert.Equal("[\"0000000000000000000000000000000000000000000000000000000000000001\",\"0000000000000000000000000000000000000000000000000000000000000002\"]", serializedScalars);

			var deserializedScalars = JsonConvert.DeserializeObject<Scalar[]>("[\"000000000000000000000000000000014551231950B75FC4402DA1732FC9BEC2\",\"0000000000000000000000000000000000000000000000000000000000000003\"]", converters);
			Assert.Equal(other, deserializedScalars[0]);
			Assert.Equal(three, deserializedScalars[1]);

			var deserializedScalarVector = JsonConvert.DeserializeObject<ScalarVector>("[\"000000000000000000000000000000014551231950B75FC4402DA1732FC9BEC2\",\"0000000000000000000000000000000000000000000000000000000000000003\"]", converters);
			Assert.Equal(deserializedScalars, deserializedScalarVector);
		}

		[Fact]
		public void RegistrationMessageSerialization()
		{
			var converters = new JsonConverter[]
			{
				new ScalarJsonConverter(),
				new GroupElementJsonConverter(),
				new MoneySatoshiJsonConverter()
			};

			using var rnd = new SecureRandom();
			var sk = new CredentialIssuerSecretKey(rnd);

			var issuer = new CredentialIssuer(sk, rnd, 4300000000000);
			var client = new WabiSabiClient(sk.ComputeCredentialIssuerParameters(), rnd, 4300000000000);
			(CredentialsRequest credentialRequest, CredentialsResponseValidation validationData) = client.CreateRequestForZeroAmount();
			var credentialResponse = issuer.HandleRequest(credentialRequest);
			var present = client.HandleResponse(credentialResponse, validationData);
			(credentialRequest, _) = client.CreateRequest(new[] { 1L }, present, CancellationToken.None);

			// Registration request message.
			var serializedRequestMessage = JsonConvert.SerializeObject(credentialRequest, converters);
			Assert.Throws<NotSupportedException>(() => JsonConvert.DeserializeObject<ZeroCredentialsRequest>(serializedRequestMessage, converters));
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
}

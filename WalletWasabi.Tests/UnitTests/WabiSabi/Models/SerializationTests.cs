using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Crypto;
using WalletWasabi.Crypto.Groups;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Crypto.CredentialRequesting;
using WalletWasabi.WabiSabi.Crypto.Serialization;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models
{
	public class SerializationTests
	{
		private static JsonConverter[] Converters =
		{
			new ScalarJsonConverter(),
			new GroupElementJsonConverter(),
			new OutPointJsonConverter(),
			new WitScriptJsonConverter(),
			new ScriptJsonConverter(),
			new OwnershipProofJsonConverter(),
		};

		private static IEnumerable<GroupElement> Points = Enumerable.Range(0, int.MaxValue).Select(i => Generators.FromText($"T{i}"));
		private static IEnumerable<Scalar> Scalars = Enumerable.Range(1, int.MaxValue).Select(i => new Scalar((uint)i));
		private static CredentialIssuerSecretKey IssuerKey = new (new InsecureRandom());

		[Fact]
		public void InputRegistrationRequestMessageSerialization()
		{
			var message = new InputRegistrationRequest(
					Guid.NewGuid(),
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
				Guid.NewGuid(),
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
				Guid.NewGuid(),
				BitcoinFactory.CreateScript(),
				CreateRealCredentialsRequest(),
				CreateRealCredentialsRequest());

			AssertSerialization(message);
		}

		[Fact]
		public void OutputRegistrationResponseMessageSerialization()
		{
			var message = new OutputRegistrationResponse(
				CreateCredentialsResponse(),
				CreateCredentialsResponse());

			AssertSerialization(message);
		}

		[Fact]
		public void ReissueCredentialRequestMessageSerialization()
		{
			var message = new ReissueCredentialRequest(
				Guid.NewGuid(),
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
				Guid.NewGuid(),
				Guid.NewGuid());

			AssertSerialization(message);
		}

		[Fact]
		public void TransactionSignatureRequestMessageSerialization()
		{
			using var key1 = new Key();
			using var key2 = new Key();
			var message = new TransactionSignaturesRequest(
				Guid.NewGuid(),
				new[]
				{
					new InputWitnessPair(1, new WitScript(Op.GetPushOp(key1.PubKey.ToBytes())) ),
					new InputWitnessPair(17, new WitScript(Op.GetPushOp(key2.PubKey.ToBytes())) )
				});

			AssertSerialization(message);
		}

		private static void AssertSerialization<T>(T message)
		{
			var serializedMessage = JsonConvert.SerializeObject(message, Converters);
			var deserializedMessage = JsonConvert.DeserializeObject<T>(serializedMessage, Converters);
			var reserializedMessage = JsonConvert.SerializeObject(deserializedMessage, Converters);
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
}

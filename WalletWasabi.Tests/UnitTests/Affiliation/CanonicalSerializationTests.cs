using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using WalletWasabi.Affiliation.Extensions;
using WalletWasabi.Affiliation.Models.CoinJoinNotification;
using WalletWasabi.Affiliation.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Affiliation;

public class CanonicalSerializationTests
{
	[Fact]
	public void DelimitersSerialization()
	{
		var data = new { a = 1 };
		string json = JsonConvert.SerializeObject(data, CanonicalJsonSerializationOptions.Settings);
		Assert.Equal("""{"a":1}""", json);
	}

	[Fact]
	public void OrderSerialization()
	{
		string json = JsonConvert.SerializeObject(new OrderSerializationJsonObject(), CanonicalJsonSerializationOptions.Settings);
		Assert.Equal("""{"1":0,"_":0,"a":0,"b":0}""", json);
	}

	[Fact]
	public void IllegalCharacterSerialization()
	{
		Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(new IllegalCharacterSerializationJsonObject(), CanonicalJsonSerializationOptions.Settings));
	}

	[Fact]
	public void TransactionSerialization()
	{
		var transaction = new Payload(
			Header.Instance,
			new Body(
					Inputs: new List<Input>()
					{
						new Input(new Outpoint(Encoders.Hex.DecodeData("e5b7e21b5ba720e81efd6bfa9f854ababdcddc75a43bfa60bf0fe069cfd1bb8a"), 0), Encoders.Hex.DecodeData("5120b3a2750e21facec36b2a56d76cca6019bf517a5c45e2ea8e5b4ed191090f3003"), true, false),
						new Input(new Outpoint(Encoders.Hex.DecodeData("f982c0a283bd65a59aa89eded9e48f2a3319cb80361dfab4cf6192a03badb60a"), 1), Encoders.Hex.DecodeData("51202f436892d90fb2665519efa3d9f0f5182859124f179486862c2cd7a78ea9ac19"), true, false)
					},
					Outputs: new List<Output>()
					{
						new Output(50000, Encoders.Hex.DecodeData("5120e0458118b80a08042d84c4f0356d86863fe2bffc034e839c166ad4e8da7e26ef")),
						new Output(50000, Encoders.Hex.DecodeData("5120bdb100a4e7ba327d364642dc653b9e6b51783bde6ea0df2ccbc1a78e3cc13295")),
						new Output(7202065, Encoders.Hex.DecodeData("5120c5c7c63798b59dc16e97d916011e99da5799d1b3dd81c2f2e93392477417e71e")),
						new Output(49010, Encoders.Hex.DecodeData("512062fdf14323b9ccda6f5b03c5c2c28e35839a3909a2e14d32b595c63d53c7b88f")),
						new Output(36945, Encoders.Hex.DecodeData("76a914a579388225827d9f2fe9014add644487808c695d88ac"))
					},
					Slip44CoinType: Network.TestNet.ToSlip44CoinType(),
					FeeRate: 0.003m,
					NoFeeThreshold: 1000000,
					MinRegistrableAmount: 5000,
					Timestamp: 0));
		string expected_json = """{"body":{"fee_rate":300000,"inputs":[{"is_affiliated":true,"is_no_fee":false,"prevout":{"hash":"e5b7e21b5ba720e81efd6bfa9f854ababdcddc75a43bfa60bf0fe069cfd1bb8a","index":0},"script_pubkey":"5120b3a2750e21facec36b2a56d76cca6019bf517a5c45e2ea8e5b4ed191090f3003"},{"is_affiliated":true,"is_no_fee":false,"prevout":{"hash":"f982c0a283bd65a59aa89eded9e48f2a3319cb80361dfab4cf6192a03badb60a","index":1},"script_pubkey":"51202f436892d90fb2665519efa3d9f0f5182859124f179486862c2cd7a78ea9ac19"}],"min_registrable_amount":5000,"no_fee_threshold":1000000,"outputs":[{"amount":50000,"script_pubkey":"5120e0458118b80a08042d84c4f0356d86863fe2bffc034e839c166ad4e8da7e26ef"},{"amount":50000,"script_pubkey":"5120bdb100a4e7ba327d364642dc653b9e6b51783bde6ea0df2ccbc1a78e3cc13295"},{"amount":7202065,"script_pubkey":"5120c5c7c63798b59dc16e97d916011e99da5799d1b3dd81c2f2e93392477417e71e"},{"amount":49010,"script_pubkey":"512062fdf14323b9ccda6f5b03c5c2c28e35839a3909a2e14d32b595c63d53c7b88f"},{"amount":36945,"script_pubkey":"76a914a579388225827d9f2fe9014add644487808c695d88ac"}],"slip44_coin_type":1,"timestamp":0},"header":{"title":"coinjoin notification","version":1}}""";

		string json = JsonConvert.SerializeObject(transaction, CanonicalJsonSerializationOptions.Settings);
		Assert.Equal(expected_json, json);
	}

	private record OrderSerializationJsonObject
	{
		[JsonProperty("a")]
		public int A { get; } = 0;

		[JsonProperty("b")]
		public int B { get; } = 0;

		[JsonProperty("_")]
		public int Underscore { get; } = 0;

		[JsonProperty("1")]
		public int One { get; } = 0;
	}

	private record IllegalCharacterSerializationJsonObject
	{
		[JsonProperty("A")]
		public int CapitalA { get; } = 0;
	}
}

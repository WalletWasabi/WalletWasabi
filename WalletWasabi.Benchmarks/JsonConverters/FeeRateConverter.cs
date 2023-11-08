using BenchmarkDotNet.Attributes;
using NBitcoin;
using WalletWasabi.JsonConverters.Bitcoin;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Benchmarks.JsonConverters;

[MemoryDiagnoser]
public class FeeRateConverter
{
	private TestData testObject;
		private string json = """{"FeePaidSize":500000,"FeePerKByte":1000,"SatoshiPerByte":1000000,"Zero":0,"NoneFeeRate":null,"NotAnnotated":null}""";

		public FeeRateConverter()
		{
			testObject = new TestData();
		}

		[Benchmark]
		public string OldConverterSerialize() => JsonConvertOld.SerializeObject(testObject);

		[Benchmark]
		public string NewConverterSerialize() => JsonConvertNew.Serialize(testObject);

		[Benchmark]
		public TestData? OldConverterDeserialize() => JsonConvertOld.DeserializeObject<TestData>(json);

		[Benchmark]
		public TestData? NewConverterDeserialize() => JsonConvertNew.Deserialize<TestData>(json);

		public record TestData
		{
			//FeeRate
			[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
			[System.Text.Json.Serialization.JsonPropertyName(nameof(FeePaidSize))]
			[Newtonsoft.Json.JsonProperty(PropertyName = nameof(FeePaidSize))]
			[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
			public FeeRate FeePaidSize { get; set; } = new (Money.Satoshis(1000), 2);

			[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
			[System.Text.Json.Serialization.JsonPropertyName(nameof(FeePerKByte))]
			[Newtonsoft.Json.JsonProperty(PropertyName = nameof(FeePerKByte))]
			[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
			public FeeRate FeePerKByte { get; set; } = new (Money.Satoshis(1000));

			[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
			[System.Text.Json.Serialization.JsonPropertyName(nameof(SatoshiPerByte))]
			[Newtonsoft.Json.JsonProperty(PropertyName = nameof(SatoshiPerByte))]
			[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
			public FeeRate SatoshiPerByte { get; set; } = new ((decimal)1000);

			[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
			[System.Text.Json.Serialization.JsonPropertyName(nameof(Zero))]
			[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Zero))]
			[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
			public FeeRate Zero { get; set; } = new(Money.Zero);

			[System.Text.Json.Serialization.JsonConverter(typeof(FeeRateJsonConverterNg))]
			[System.Text.Json.Serialization.JsonPropertyName(nameof(NoneFeeRate))]
			[Newtonsoft.Json.JsonProperty(PropertyName = nameof(NoneFeeRate))]
			[Newtonsoft.Json.JsonConverter(typeof(FeeRateJsonConverter))]
			public FeeRate? NoneFeeRate { get; set; } = null;

			/// <summary>Not annotated properties are also included by both Newtonsoft and STJ by default.</summary>
			public Money? NotAnnotated { get; set; } = null;
		}
}

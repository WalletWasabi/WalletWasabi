using NBitcoin;
using System.Diagnostics;
using System.IO;
using WalletWasabi.Helpers;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Models.Serialization;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

public class MoneyBtcJsonConverterCompareTests
{
	[Fact]
	public void STJCOnverterWorksLikeNewtonsoft()
	{
		string directory = Common.GetWorkDir();
		IoHelpers.EnsureDirectoryExists(directory);

		string newtonsoftFilePath = Path.Combine(directory, "newtonsoft.json");
		string stjFilePath = Path.Combine(directory, "stj.json");

		var stjClass = new STJJsonSerializableClass();
		var newtonClass = new NewtonsoftJsonSerializableClass();

		// Measure Newtonsoft's serilize and deserialize.
		var stopwatch = Stopwatch.StartNew();

		string newtonJson = Newtonsoft.Json.JsonConvert.SerializeObject(newtonClass);
		File.WriteAllText(newtonsoftFilePath, newtonJson);

		newtonJson = File.ReadAllText(newtonsoftFilePath);
		var deserializedNewtonClass = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftJsonSerializableClass>(newtonJson);
		stopwatch.Stop();
		var newtontime = stopwatch.ElapsedMilliseconds;

		// Sanity checks.
		Assert.Equal(Money.Coins(1m), deserializedNewtonClass.OneBTC);
		Assert.Equal(Money.Coins(0.5m), deserializedNewtonClass.HalfBTC);
		Assert.Equal(Money.Satoshis(1000), deserializedNewtonClass.SomeSats);

		// Measure Microsoft's serilize and deserialize.
		stopwatch = Stopwatch.StartNew();

		string stjJson = System.Text.Json.JsonSerializer.Serialize(stjClass);
		// string stjJson = System.Text.Json.JsonSerializer.Serialize(stjClass, typeof(STJJsonSerializableClass)); -> Doesn't make a difference in current state.
		File.WriteAllText(stjFilePath, stjJson);

		stjJson = File.ReadAllText(stjFilePath);
		var deserializedJstClass = System.Text.Json.JsonSerializer.Deserialize<STJJsonSerializableClass>(stjJson);
		stopwatch.Stop();
		var stjtime = stopwatch.ElapsedMilliseconds;

		// newtontime = 7-8ms | stjtime = 41-52ms.
		Assert.Equal(Money.Coins(1m), deserializedJstClass.OneBTC);
		Assert.Equal(Money.Coins(0.5m), deserializedJstClass.HalfBTC);
		Assert.Equal(Money.Satoshis(1000), deserializedJstClass.SomeSats);

		// Error handling.(Neither of these throws exception)
		// SomeSats property is mistyped:
		string badJson = "{\"HalfBTC\":\"0.50\",\"OneBTC\":\"1.00\",\"SomeS\":\"0.00002\"}";
		var des = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftJsonSerializableClass>(badJson); // SomeSats is null.
		var des2 = System.Text.Json.JsonSerializer.Deserialize<STJJsonSerializableClass>(badJson); // SomeSats is 0.00001. STJ fills up the property with the init value if it can't find the new value in json.

		// A bonus property is in the middle of the json:
		badJson = "{\"HalfBTC\":\"0.50\",\"OneBTC\":\"1.00\",\"FakeSomeSats\":\"0.00003\",\"SomeSats\":\"0.00002\"}";
		des = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftJsonSerializableClass>(badJson); // Ignores FakeSomeSats, SomeSats is 0.00002.
		des2 = System.Text.Json.JsonSerializer.Deserialize<STJJsonSerializableClass>(badJson); // Ignores FakeSomeSats, SomeSats is 0.00002.

		// SomeSats is null:
		badJson = "{\"HalfBTC\":\"0.50\",\"OneBTC\":\"1.00\",\"SomeSats\":null}";
		des = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftJsonSerializableClass>(badJson); // SomeSats is null.
		des2 = System.Text.Json.JsonSerializer.Deserialize<STJJsonSerializableClass>(badJson); // SomeSats is null.

		// OneBTC has an extra zero and misses number after decimal point:
		badJson = "{\"HalfBTC\":\"0.50\",\"OneBTC\":\"01.\",\"SomeSats\":null}";
		des = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftJsonSerializableClass>(badJson); // OneBTC is 1.00000000.
		des2 = System.Text.Json.JsonSerializer.Deserialize<STJJsonSerializableClass>(badJson); // OneBTC is 1.00000000.
	}

	private class STJJsonSerializableClass
	{
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName("HalfBTC")]
		public Money HalfBTC { get; set; } = Money.Coins(0.500m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName("OneBTC")]
		public Money OneBTC { get; set; } = Money.Coins(1m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName("SomeSats")]
		public Money SomeSats { get; set; } = Money.Satoshis(1000);
	}

	private class NewtonsoftJsonSerializableClass
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = "HalfBTC", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate)]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money HalfBTC { get; set; } = Money.Coins(0.500m);

		[Newtonsoft.Json.JsonProperty(PropertyName = "OneBTC", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate)]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money OneBTC { get; set; } = Money.Coins(1m);

		[Newtonsoft.Json.JsonProperty(PropertyName = "SomeSats", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate)]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money SomeSats { get; set; } = Money.Satoshis(1000);
	}
}

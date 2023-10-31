using NBitcoin;
using WalletWasabi.JsonConverters.Bitcoin;
using Xunit;
using JsonConvertNew = System.Text.Json.JsonSerializer;
using JsonConvertOld = Newtonsoft.Json.JsonConvert;

namespace WalletWasabi.Tests.UnitTests.JsonConverters;

/// <summary>
/// Tests for <see cref="MoneyBtcJsonConverter"/> and <see cref="MoneyBtcJsonConverterNg"/> classes.
/// </summary>
public class MoneyBtcJsonConverterTests
{
	/// <summary>
	/// Tests that JSON converter based on <c>System.Text.Json</c> and the one based on NewtonSoft.JSON works the same.
	/// </summary>
	[Fact]
	public void FunctionalParity()
	{
		TestData testObject = new();

		string json = AssertSerializedEqually(testObject);
		Assert.Equal("""{"Half":"0.50","One":"1.00","Zeros":"0.000001","None":null,"NotAnnotated":null}""", json);

		//string newtonJson = JsonConvertOld.SerializeObject(testObject);
		//var deserializedNewtonClass = JsonConvertOld.DeserializeObject<TestClass>(newtonJson);

		//// Sanity checks.
		//Assert.Equal(Money.Coins(1m), deserializedNewtonClass.OneBTC);
		//Assert.Equal(Money.Coins(0.5m), deserializedNewtonClass.HalfBTC);
		//Assert.Equal(Money.Satoshis(1000), deserializedNewtonClass.SomeSats);

		//string stjJson = JsonConvertNew.Serialize(stjClass);
		//// string stjJson = System.Text.Json.JsonSerializer.Serialize(stjClass, typeof(STJJsonSerializableClass)); -> Doesn't make a difference in current state.

		//var deserializedJstClass = JsonConvertNew.Deserialize<TestClass>(stjJson);

		//// newtontime = 7-8ms | stjtime = 41-52ms.
		//Assert.Equal(Money.Coins(1m), deserializedJstClass.OneBTC);
		//Assert.Equal(Money.Coins(0.5m), deserializedJstClass.HalfBTC);
		//Assert.Equal(Money.Satoshis(1000), deserializedJstClass.SomeSats);

		//// Error handling.(Neither of these throws exception)
		//// SomeSats property is mistyped:
		//string badJson = """{"HalfBTC":"0.50","OneBTC":"1.00","SomeS":"0.00002"}""";
		//var des = JsonConvertOld.DeserializeObject<TestClass>(badJson); // SomeSats is null.
		//var des2 = JsonConvertNew.Deserialize<TestClass>(badJson); // SomeSats is 0.00001. STJ fills up the property with the init value if it can't find the new value in json.

		//// A bonus property is in the middle of the json:
		//badJson = """{"HalfBTC":"0.50","OneBTC":"1.00","FakeSomeSats":"0.00003","SomeSats":"0.00002"}""";
		//des = JsonConvertOld.DeserializeObject<TestClass>(badJson); // Ignores FakeSomeSats, SomeSats is 0.00002.
		//des2 = JsonConvertNew.Deserialize<TestClass>(badJson); // Ignores FakeSomeSats, SomeSats is 0.00002.

		//// SomeSats is null:
		//badJson = """{"HalfBTC":"0.50","OneBTC":"1.00","SomeSats":null}""";
		//des = JsonConvertOld.DeserializeObject<TestClass>(badJson); // SomeSats is null.
		//des2 = JsonConvertNew.Deserialize<TestClass>(badJson); // SomeSats is null.

		//// OneBTC has an extra zero and misses number after decimal point:
		//badJson = """{"HalfBTC":"0.50","OneBTC":"01.","SomeSats":null}""";
		//des = JsonConvertOld.DeserializeObject<TestClass>(badJson); // OneBTC is 1.00000000.
		//des2 = JsonConvertNew.Deserialize<TestClass>(badJson); // OneBTC is 1.00000000.
	}

	/// <summary>
	/// Asserts that object <paramref name="o"/> is serialized to the same JSON by both Newtonsoft library and STJ library.
	/// </summary>
	/// <returns>JSON representation of <paramref name="o"/>.</returns>
	private static string AssertSerializedEqually<T>(T o)
	{
		string newtonsoftJson = JsonConvertOld.SerializeObject(o);
		string stjJson = JsonConvertNew.Serialize(o);

		Assert.NotNull(newtonsoftJson);
		Assert.NotNull(stjJson);
		Assert.Equal(newtonsoftJson, stjJson);

		return stjJson;
	}

	/// <summary>
	/// Record with both STJ and Newtonsoft attributes.
	/// </summary>
	private record TestData
	{
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Half))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Half))]
		public Money Half { get; set; } = Money.Coins(0.500m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(One))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(One))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money One { get; set; } = Money.Coins(1m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(Zeros))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(Zeros))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money Zeros { get; set; } = Money.Coins(0.000001m);

		[System.Text.Json.Serialization.JsonConverter(typeof(MoneyBtcJsonConverterNg))]
		[System.Text.Json.Serialization.JsonPropertyName(nameof(None))]
		[Newtonsoft.Json.JsonProperty(PropertyName = nameof(None))]
		[Newtonsoft.Json.JsonConverter(typeof(MoneyBtcJsonConverter))]
		public Money? None { get; set; } = null;

		/// <summary>Not annotated properties are also included by both Newtonsoft and STJ by default.</summary>
		public Money? NotAnnotated { get; set; } = null;
	}
}
